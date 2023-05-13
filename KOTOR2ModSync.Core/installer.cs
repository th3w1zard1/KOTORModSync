using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

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

        public async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod)
        {
            return await instructionMethod().ConfigureAwait(false);
        }

        public async Task<bool> ExtractFileAsync(Instruction thisStep, Component component)
        {
            // Implement extraction logic here
            return true;
        }

        public async Task<bool> DeleteFileAsync(Instruction thisStep, Component component)
        {
            // Implement deletion logic here
            return true;
        }

        public async Task<bool> MoveFileAsync(Instruction thisStep, Component component)
        {
            // Implement moving logic here
            return true;
        }

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

                var process = new Process();
                process.StartInfo.FileName = thisStep.Path;
                process.StartInfo.Arguments = thisStep.Arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();

                while (!process.HasExited && !cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    throw new Exception("TSLPatcher timed out after 30 seconds.");
                }

                var output = await outputTask;

                if (process.ExitCode != 0)
                {
                    throw new Exception($"TSLPatcher failed with exit code {process.ExitCode}. Output:\n{output}");
                }

                if (!VerifyInstall(component))
                {
                    throw new Exception("TSLPatcher failed to install the mod correctly.");
                }

                return true;
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occurred
                // Log the exception or display it to the user as appropriate
                return false;
            }
        }

        public static bool VerifyInstall(Component component)
        {
            // Verify if the destination directory has been modified
            DateTime destinationDirectoryLastModified = Directory.GetLastWriteTime(MainConfig.DestinationPath.FullName);

            /*if (destinationDirectoryLastModified < component.SourceLastModified)
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
        private bool isChecked;

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name { get; set; }
        public string Guid { get; set; }
        public int InstallOrder { get; set; }
        public List<string> Dependencies { get; set; }
        public List<Instruction> Instructions { get; set; }

        public Component DeserializeComponent(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTable componentTable))
                throw new ArgumentException("Expected a TOML table for component data.");

            var componentDict = ConvertTomlTableToDictionary(componentTable);

            var component = new Component
            {
                Name = GetRequiredValue<string>(componentDict, "Name"),
                Guid = GetRequiredValue<string>(componentDict, "Guid"),
                InstallOrder = GetValueOrDefault<int>(componentDict, "InstallOrder"),
                Dependencies = GetValueOrDefault<List<object>>(componentDict, "Dependencies")?.Select(x => x.ToString()).ToList(),
                Instructions = GetValueOrDefault<List<TomlObject>>(componentDict, "Instructions")?.Select(x => DeserializeInstruction(x)).ToList()
            };
            component.Instructions?.ForEach(instruction => instruction.ParentComponent = component);

            return component;
        }

        private Instruction DeserializeInstruction(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTable instructionTable))
                throw new ArgumentException("Expected a TOML table for instruction data.");

            var instructionDict = ConvertTomlTableToDictionary(instructionTable);

            var instruction = new Instruction
            {
                Type = GetRequiredValue<string>(instructionDict, "Type"),
                Source = GetRequiredValue<string>(instructionDict, "Source"),
                Destination = GetRequiredValue<string>(instructionDict, "Destination"),
                Overwrite = GetValueOrDefault<bool>(instructionDict, "Overwrite"),
                Path = GetValueOrDefault<string>(instructionDict, "Path"),
                Arguments = GetValueOrDefault<string>(instructionDict, "Arguments"),
                ParentComponent = null
            };

            return instruction;
        }

        private static Dictionary<string, object> ConvertTomlTableToDictionary(TomlTable tomlTable)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            foreach (var kvp in tomlTable)
            {
                string key = kvp.Key.ToString();
                object value = kvp.Value;

                dict.Add(key, value);
            }

            return dict;
        }

        private T GetRequiredValue<T>(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object value) || !(value is T))
                throw new ArgumentException($"Missing or invalid '{key}' field.");

            return (T)value;
        }

        private T GetValueOrDefault<T>(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object value) && value is T)
                return (T)value;

            return default(T);
        }

        public async Task<bool> ExecuteInstructions()
        {
            // Check if we have permission to write to the Destination directory
            if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath))
            {
                throw new Exception("Cannot write to the destination directory.");
            }

            string modConfigFile = Path.Combine(MainConfig.ModConfigPath.FullName, "modpack.toml");
            List<Component> components = Utility.Serializer.FileHandler.ReadComponentsFromFile(modConfigFile);

            foreach (Component component in components)
            {
                foreach (Instruction instruction in component.Instructions)
                {
                    // Get the original checksums before making any modifications
                    var originalPathsToChecksum = new Dictionary<FileInfo, System.Security.Cryptography.SHA1>();
                    try
                    {
                        foreach (var file in MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories))
                        {
                            System.Security.Cryptography.SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(file);
                            originalPathsToChecksum[file] = sha1;
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Failed to access files in destination directory: {ex.Message}");
                        return false;
                    }

                    bool success = false;

                    switch (instruction.Type.ToLower())
                    {
                        case "extract":
                            success = await instruction.ExtractFileAsync(instruction, component);
                            break;

                        case "delete":
                            success = await instruction.DeleteFileAsync(instruction, component);
                            break;

                        case "move":
                            success = await instruction.MoveFileAsync(instruction, component);
                            break;

                        case "tslpatcher":
                            success = await instruction.ExecuteTSLPatcherAsync(instruction, component);
                            break;

                        default:
                            // Handle unknown instruction type here
                            break;
                    }

                    if (success)
                    {
                        // Get the new checksums after the modifications
                        var validator = new FileChecksumValidator(
                            destinationPath: MainConfig.DestinationPath.FullName,
                            expectedChecksums: instruction.ExpectedChecksums,
                            originalChecksums: originalPathsToChecksum
                        );

                        var checksumsMatch = await validator.ValidateChecksumsAsync();

                        if (checksumsMatch)
                        {
                            Console.WriteLine($"Instruction {instruction.Type} succeeded and modified files have expected checksums.");
                        }
                        else
                        {
                            Console.WriteLine($"Instruction {instruction.Type} succeeded but modified files have unexpected checksums.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Instruction {instruction.Type} failed to install the mod correctly.");
                    }
                }
                string outModConfig = Path.Combine(MainConfig.ModConfigPath.FullName, "modpack_new.toml");
            }

            return true;
        }
    }
}
