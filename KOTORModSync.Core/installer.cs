// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using Tomlyn.Model;

namespace KOTORModSync.Core
{
    public class Instruction
    {
        public string Type { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public bool Overwrite { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public Component ParentComponent { get; set; }
        public Dictionary<FileInfo, System.Security.Cryptography.SHA1> ExpectedChecksums { get; set; }

        public static async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod) => await instructionMethod().ConfigureAwait(false);

        public bool ExtractFile(Instruction thisStep, Component component) =>
            // Implement extraction logic here
            true;

        public bool DeleteFile(Instruction thisStep, Component component) =>
            // Implement deletion logic here
            true;

        public bool MoveFile(Instruction thisStep, Component component) =>
            // Implement moving logic here
            true;

        public async Task<bool> ExecuteTSLPatcherAsync(Instruction thisStep, Component component)
        {
            try
            {
                // Check if we have permission to write to the Destination directory
                if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath))
                {
                    throw new Exception("Cannot write to the destination directory.");
                }

                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

                var startInfo = new ProcessStartInfo
                {
                    FileName = thisStep.Path,
                    Arguments = thisStep.Arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = new Process
                {
                    StartInfo = startInfo
                };
                _ = process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();

                while (!process.HasExited && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    throw new Exception("TSLPatcher timed out after 30 seconds.");
                }

                string output = await outputTask;

                return process.ExitCode != 0
                    ? throw new Exception($"TSLPatcher failed with exit code {process.ExitCode}. Output:\n{output}")
                    : !VerifyInstall(component) ? throw new Exception("TSLPatcher failed to install the mod correctly.") : true;
            }
            catch (Exception)
            {
                // Handle any exceptions that occurred
                // Log the exception or display it to the user as appropriate
                return false;
            }
        }

        public static bool VerifyInstall(Component component)
        {
            // Verify if the destination directory has been modified
            /*DateTime destinationDirectoryLastModified = Directory.GetLastWriteTime(MainConfig.DestinationPath.FullName);

            if (destinationDirectoryLastModified < component.SourceLastModified)
            {
                Console.WriteLine("Destination directory has not been modified.");
                return false;
            }*/

            // Verify if any error or warning message is present in the install.rtf file
            string installLogFile = System.IO.Path.Combine(MainConfig.DestinationPath.FullName, "install.rtf");

            if (!File.Exists(installLogFile))
            {
                Console.WriteLine("Install log file not found.");
                return false;
            }

            string installLogContent = File.ReadAllText(installLogFile);
            string[] bulletPoints = installLogContent.Split('\u2022');

            foreach (string bulletPoint in bulletPoints)
            {
                if (bulletPoint.Contains("Warning") || bulletPoint.Contains("Error"))
                {
                    Console.WriteLine($"Install log contains warning or error message: {bulletPoint.Trim()}");
                    return false;
                }
            }

            return true;
        }
    }

    public class Component
    {
        private bool _isChecked;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Name { get; set; }
        public string Guid { get; set; }
        public int InstallOrder { get; set; }
        public List<string> Dependencies { get; set; }
        public List<Instruction> Instructions { get; set; }
        public DateTime SourceLastModified { get; internal set; }

        public static Component DeserializeComponent(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTable componentTable))
            {
                throw new ArgumentException("Expected a TOML table for component data.");
            }

            Dictionary<string, object> componentDict = ConvertTomlTableToDictionary(componentTable);

            var component = new Component
            {
                Name = GetRequiredValue<string>(componentDict, "Name"),
                Guid = GetRequiredValue<string>(componentDict, "Guid"),
                InstallOrder = GetValueOrDefault<int>(componentDict, "InstallOrder"),
                Dependencies = GetValueOrDefault<List<string>>(componentDict, "Dependencies"),
                Instructions = DeserializeInstructions(GetRequiredValue<TomlTableArray>(componentDict, "Instructions"))
            };
            component.Instructions?.ForEach(instruction => instruction.ParentComponent = component);

            return component;
        }

        private static List<Instruction> DeserializeInstructions(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTableArray instructionsArray))
            {
                throw new ArgumentException("Expected a TOML table array for instructions data.");
            }

            var instructions = new List<Instruction>();

            foreach (var item in instructionsArray)
            {
                if (item is TomlTable instructionTable)
                {
                    Dictionary<string, object> instructionDict = ConvertTomlTableToDictionary(instructionTable);
                    var instruction = new Instruction
                    {
                        Type = GetRequiredValue<string>(instructionDict, "Type"),
                        Source = GetValueOrDefault<string>(instructionDict, "Source"),
                        Destination = GetValueOrDefault<string>(instructionDict, "Destination"),
                        Overwrite = GetValueOrDefault<bool>(instructionDict, "Overwrite"),
                        Path = GetValueOrDefault<string>(instructionDict, "Path"),
                        Arguments = GetValueOrDefault<string>(instructionDict, "Arguments"),
                        ParentComponent = null
                    };

                    instructions.Add(instruction);
                }
            }

            return instructions;
        }


        private static Dictionary<string, object> ConvertTomlTableToDictionary(TomlTable tomlTable)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            foreach (KeyValuePair<string, object> kvp in tomlTable)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (value is TomlTable nestedTable)
                {
                    dict.Add(key, ConvertTomlTableToDictionary(nestedTable));
                }
                else
                {
                    dict.Add(key, value);
                }
            }

            return dict;
        }
        private static T GetRequiredValue<T>(Dictionary<string, object> dict, string key) => GetValue<T>(dict, key, true);

        private static T GetValueOrDefault<T>(Dictionary<string, object> dict, string key) => GetValue<T>(dict, key, false);

        private static T GetValue<T>(Dictionary<string, object> dict, string key, bool required)
        {
            if (!dict.TryGetValue(key, out object value))
            {
                var caseInsensitiveKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (caseInsensitiveKey == null)
                {
                    if (required)
                    {
                        throw new ArgumentException($"Missing or invalid '{key}' field.");
                    }
                    else
                    {
                        return default(T);
                    }
                }
                value = dict[caseInsensitiveKey];
            }

            if (value is T t)
            {
                return t;
            }

            if (value is Dictionary<string, object> nestedDict)
            {
                return GetValue<T>(nestedDict, key, required); // Recursively look for the key in the nested dictionary
            }

            if (value is TomlArray tomlArray && typeof(T) == typeof(List<string>))
            {
                return (T)(object)tomlArray.Select(x => x.ToString()).ToList<string>(); // Convert TomlArray to List<string>
            }

            if (value is KeyValuePair<string, object> kvp && kvp.Value is T t2)
            {
                return t2;
            }

            if (required)
            {
                throw new ArgumentException($"Missing or invalid '{key}' field.");
            }

            return default(T);
        }

        public static async Task<bool> ExecuteInstructions()
        {
            try
            {
                // Check if we have permission to write to the Destination directory
                if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath))
                {
                    throw new Exception("Cannot write to the destination directory.");
                }

                async Task<bool> ProcessComponentAsync(Component component)
                {
                    foreach (Instruction instruction in component.Instructions)
                    {
                        // Get the original checksums before making any modifications
                        var originalPathsToChecksum = new Dictionary<FileInfo, System.Security.Cryptography.SHA1>();
                        foreach (FileInfo file in MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories))
                        {
                            System.Security.Cryptography.SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(file);
                            originalPathsToChecksum[file] = sha1;
                        }

                        bool success = false;

                        switch (instruction.Type.ToLower())
                        {
                            case "extract":
                                success = instruction.ExtractFile(instruction, component);
                                break;

                            case "delete":
                                success = instruction.DeleteFile(instruction, component);
                                break;

                            case "move":
                                success = instruction.MoveFile(instruction, component);
                                break;

                            case "tslpatcher":
                                success = await instruction.ExecuteTSLPatcherAsync(instruction, component);
                                break;

                            default:
                                // Handle unknown instruction type here
                                break;
                        }

                        if (!success)
                        {
                            Console.WriteLine($"Instruction {instruction.Type} failed to install the mod correctly.");
                            return false;
                        }

                        // Get the new checksums after the modifications
                        var validator = new FileChecksumValidator(
                            destinationPath: MainConfig.DestinationPath.FullName,
                            expectedChecksums: instruction.ExpectedChecksums,
                            originalChecksums: originalPathsToChecksum
                        );

                        bool checksumsMatch = await validator.ValidateChecksumsAsync();

                        if (checksumsMatch)
                        {
                            Console.WriteLine($"Instruction {instruction.Type} succeeded and modified files have expected checksums.");
                        }
                        else
                        {
                            Console.WriteLine($"Instruction {instruction.Type} succeeded but modified files have unexpected checksums.");
                        }
                    }

                    _ = Path.Combine(MainConfig.ModConfigPath.FullName, "modpack_new.toml");

                    return true;
                }

                string modConfigFile = Path.Combine(MainConfig.ModConfigPath.FullName, "modpack.toml");
                List<Component> components = Utility.Serializer.FileHandler.ReadComponentsFromFile(modConfigFile);

                foreach (Component component in components)
                {
                    bool result = await ProcessComponentAsync(component);

                    if (!result)
                    {
                        Console.WriteLine($"Component {component.Name} failed to install the mod correctly.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred: {ex.Message}");
                return false;
            }
        }
    }
}
