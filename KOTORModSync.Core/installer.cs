// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Tomlyn.Model;
using static KOTORModSync.Core.Utility.Utility;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using static KOTORModSync.Core.ModDirectory;
using static KOTORModSync.Core.Utility.Serializer;

namespace KOTORModSync.Core
{
    public class Instruction
    {
        public string Action { get; set; }
        public List<string> Source { get; set; }
        public string Destination { get; set; }
        public List<string> Dependencies { get; set; }
        public List<string> Restrictions { get; set; }
        public bool Overwrite { get; set; }
        public List<string> Paths { get; set; }
        public string Arguments { get; set; }
        public Component ParentComponent { get; set; }
        public Dictionary<FileInfo, SHA1> ExpectedChecksums { get; set; }
        public Dictionary<FileInfo, SHA1> OriginalChecksums { get; internal set; }

        public static string defaultInstructions = @"
[[thisMod.instructions]]
action = ""extract""
source = ""<<modDirectory>>\\path\\to\\mod\\mod.rar""
overwrite = true

[[thisMod.instructions]]
action = ""delete""
paths = [
    ""<<modDirectory>>\\path\\to\\mod\\file1.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file2.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file3.tpc""
]
dependencies = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""
overwrite = false

[[thisMod.instructions]]
action = ""move""
source = ""<<modDirectory>>\\path\\to\\mod\\file\\to\\move""
destination = ""C:\\Users\\****\\path\\to\\kotor2\\Override""
restrictions = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""

[[thisMod.instructions]]
action = ""run""
Source = [""<<modDirectory>>\\path\\to\\mod\\program.exe""]
arguments = ""any command line arguments to pass""
# same as 'run' except it'll try to verify the installation from the tslpatcher log.

[[thisMod.instructions]]
action = ""tslpatcher""
source = [""<<modDirectory>>\\path\\to\\mod\\TSLPatcher.exe""]
arguments = ""any command line arguments to pass (none available in TSLPatcher)""
";

        public static async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod) => await instructionMethod().ConfigureAwait(false);

        public bool ExtractFile()
        {
            try
            {
                foreach (string sourcePath in Source)
                {
                    var thisFile = new FileInfo(sourcePath);
                    Logger.Log($"File path: {thisFile.FullName}");

                    if (ArchiveHelper.IsArchive(thisFile.Extension))
                    {
                        List<ArchiveEntry> archiveEntries = ArchiveHelper.TraverseArchiveEntries(thisFile.FullName);

                        foreach (ArchiveEntry entry in archiveEntries)
                        {
                            string destinationFolder = Path.GetFileNameWithoutExtension(thisFile.Name);
                            string destinationPath = Path.Combine(thisFile.Directory.FullName, destinationFolder, entry.Path);
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            using (Stream outputStream = File.Create(destinationPath))
                            {
                                using (FileStream stream = File.OpenRead(thisFile.FullName))
                                {
                                    IArchive archive = null;

                                    if (thisFile.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                                    {
                                        archive = SharpCompress.Archives.Zip.ZipArchive.Open(stream);
                                    }
                                    else if (thisFile.Extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                                    {
                                        archive = RarArchive.Open(stream);
                                    }
                                    else if (thisFile.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                                    {
                                        archive = SevenZipArchive.Open(stream);
                                    }
                                    Logger.Log($"Attempting to extract {thisFile.Name}");
                                    if (archive != null)
                                    {
                                        IArchiveEntry archiveEntry = archive.Entries.FirstOrDefault(e => e.Key == entry.Path);
                                        if (archiveEntry != null && !archiveEntry.IsDirectory)
                                        {
                                            Logger.Log($"Extracting {archiveEntry.Key}");
                                            using (Stream entryStream = archiveEntry.OpenEntryStream())
                                            {
                                                entryStream.CopyTo(outputStream);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var ex = new ArgumentNullException($"{this.ParentComponent.Name} failed to extract file {thisFile}");
                        Logger.LogException(ex);
                        throw ex;
                    }
                }

                return true; // Extraction succeeded
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occurred during extraction
                Logger.LogException(ex);
                return false; // Extraction failed
            }
        }

        public async Task<bool> DeleteFile()
        {
            try
            {
                var deleteTasks = new List<Task>();
                int maxDegreeOfParallelism = await Utility.PlatformAgnosticMethods.CalculateMaxDegreeOfParallelismAsync(new DirectoryInfo(this.Source[0]));
                SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism); // Set the maximum degree of parallelism

                for (int i = 0; i < this.Source.Count; i++)
                {
                    var thisFile = new FileInfo(this.Source[i]);
                    if (Path.IsPathRooted(thisFile.FullName))
                    {
                        // Delete the file asynchronously
                        Task deleteTask = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync(); // Acquire a semaphore slot

                            try
                            {
                                File.Delete(thisFile.FullName);
                                Logger.Log($"Deleting {thisFile.FullName}...");
                            }
                            finally
                            {
                                semaphore.Release(); // Release the semaphore slot
                            }
                        });

                        deleteTasks.Add(deleteTask);
                    }
                    else
                    {
                        var ex = new ArgumentException($"Invalid wildcards/not a valid path: {thisFile.FullName}");
                        Logger.LogException(ex);
                        return false;
                    }
                }

                // Wait for all delete tasks to complete
                await Task.WhenAll(deleteTasks);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }


        public async Task<bool> MoveFile() {
            try {
                var moveTasks = new List<Task>();
                int maxDegreeOfParallelism = await Utility.PlatformAgnosticMethods.CalculateMaxDegreeOfParallelismAsync(new DirectoryInfo(this.Source[0]));
                SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism); // Set the maximum degree of parallelism

                for (int i = 0; i < Source.Count; i++) {
                    var thisFile = new FileInfo(this.Source[i]);
                    var destinationPath = new DirectoryInfo(this.Destination);
                    // Check if the destination file already exists
                    if (this.Overwrite || !File.Exists(Path.Combine(destinationPath.FullName, thisFile.Name)))
                    {
                        // Move the file asynchronously
                        Task moveTask = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync(); // Acquire a semaphore slot

                            try {
                                await Serializer.FileHandler.MoveFileAsync(thisFile.FullName, destinationPath.FullName);
                                Logger.Log($"Moving {thisFile.FullName} to {destinationPath}... Overwriting? {this.Overwrite}");
                            } finally {
                                semaphore.Release(); // Release the semaphore slot
                            }
                        });

                        moveTasks.Add(moveTask);
                    }
                }

                // Wait for all move tasks to complete
                await Task.WhenAll(moveTasks);

                return true;
            } catch (Exception ex) {
                Logger.LogException(ex);
                return false;
            }
        }

        public async Task<bool> ExecuteProgramAsync()
        {
            try
            {
                bool isSuccess = false; // Track the success status
                for (int i = 0; i < this.Source.Count; i++)
                {
                    var thisProgram = new FileInfo(this.Source[i]);
                    if (!thisProgram.Exists)
                    {
                        throw new FileNotFoundException($"The file {this.Source[i]} could not be located on the disk");
                    }
                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3600)); // cancel if running longer than 30 minutes.
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = thisProgram.FullName,
                        Arguments = Arguments,
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
                        throw new Exception("Process timed out after 30 minutes.");
                    }

                    string output = await outputTask;

                    if (process.ExitCode != 0)
                    {
                        Logger.Log($"Process failed with exit code {process.ExitCode}. Output:\n{output}");
                        isSuccess = false;
                    }
                    else
                    {
                        isSuccess = true;
                    }
                }
                return isSuccess;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }

        public bool VerifyInstall()
        {
            bool errors = false;
            bool found = false;
            foreach(string sourcePath in this.Source)
            {
                // Verify if any error or warning message is present in the install.rtf file
                string installLogFile = System.IO.Path.Combine(Path.GetDirectoryName(sourcePath), "install.rtf");

                if (!File.Exists(installLogFile))
                {
                    Logger.Log("Install log file not found.");
                    continue;
                }

                found = false;
                string installLogContent = File.ReadAllText(installLogFile);
                string[] bulletPoints = installLogContent.Split('\u2022');

                foreach (string bulletPoint in bulletPoints)
                {
                    if (bulletPoint.Contains("Error"))
                    {
                        Logger.Log($"Install log contains warning or error message: {bulletPoint.Trim()}");
                        errors = true;
                    }
                }
            }
            return found && !errors;
        }
    }

    public class Component
    {
        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Name { get; set; }
        public string Guid { get; set; }
        public int InstallOrder { get; set; }
        public List<string> Dependencies { get; set; }
        public List<string> Restrictions { get; set; }
        public List<Instruction> Instructions { get; set; }
        public string Author { get; set; }
        public string Tier { get; set; }
        public string Directions { get; set; }
        public string Description { get; set; }
        public List<string> Language { get; set; }
        public string Category { get; set; }
        public DateTime SourceLastModified { get; internal set; }

        public static string defaultComponent = @"
[[thisMod]]
    name = ""the name of your mod""
    # Use the button below to generate a Global Unique Identifier (guid) for this mod
    guid = ""{01234567-ABCD-EF01-2345-6789ABCDEF01}""
    # Copy and paste any guid of any mod you depend on here, format like below
    dependencies = [
        ""{d2bf7bbb-4757-4418-96bf-a9772a36a262}"",
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""
    ]
    # Copy and paste any guid of any incompatible mod here, format like below
    restrictions = [
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
        ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
    ]
    installOrder = 3";

        public DirectoryInfo tempPath;

        public void DeserializeComponent(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTable componentTable))
            {
                throw new ArgumentException("Expected a TOML table for component data.");
            }

            tempPath = new DirectoryInfo(Path.GetTempPath());

            Dictionary<string, object> componentDict = ConvertTomlTableToDictionary(componentTable);
            List<string> paths = new List<string>();
            if (componentDict.TryGetValue("path", out object pathValue) && pathValue is string path)
            {
                paths.Add(path);
            }
            DeserializePath(componentDict, "paths");

            this.Name = GetRequiredValue<string>(componentDict, "name");
            this.Guid = GetRequiredValue<string>(componentDict, "guid");
            this.InstallOrder = GetValueOrDefault<int>(componentDict, "installorder");
            this.Description = GetValueOrDefault<string>(componentDict,"description");
            this.Directions = GetValueOrDefault<string>(componentDict, "directions");
            this.Category = GetValueOrDefault<string>(componentDict, "category");
            this.Tier = GetValueOrDefault<string>(componentDict, "tier");
            this.Language = GetValueOrDefault<List<string>>(componentDict, "language");
            this.Author = GetValueOrDefault<string>(componentDict, "author");
            this.Dependencies = GetValueOrDefault<List<string>>(componentDict, "dependencies");
            this.Instructions = DeserializeInstructions(GetValueOrDefault<TomlTableArray>(componentDict, "instructions"));

            this.Instructions?.ForEach(instruction => instruction.ParentComponent = this);
        }

        private static List<Instruction> DeserializeInstructions(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTableArray instructionsArray))
            {
                Logger.LogException(new Exception("Expected a TOML table array for instructions data."));
                return null;
            }

            var instructions = new List<Instruction>();

            foreach (TomlTable item in instructionsArray)
            {
                if (item is TomlTable instructionTable)
                {
                    Dictionary<string, object> instructionDict = ConvertTomlTableToDictionary(instructionTable);

                    // ConvertTomlTableToDictionary lowercases all string keys.
                    DeserializePath(instructionDict, "source");
                    if (!instructionDict.ContainsKey("destination"))
                    {
                        instructionDict["destination"] = "<<kotorDirectory>>/Override";
                    }
                    DeserializeGuids(instructionDict, "restrictions");
                    DeserializeGuids(instructionDict, "dependencies");
                    var instruction = new Instruction
                    {
                        Action = GetRequiredValue<string>(instructionDict, "action"),
                        Source = GetValueOrDefault<List<string>>(instructionDict, "source"),
                        Destination = GetValueOrDefault<string>(instructionDict, "destination"),
                        Dependencies = GetValueOrDefault<List<string>>(instructionDict, "dependencies"),
                        Restrictions = GetValueOrDefault<List<string>>(instructionDict, "restrictions"),
                        Overwrite = GetValueOrDefault<bool>(instructionDict, "overwrite"),
                        Arguments = GetValueOrDefault<string>(instructionDict, "arguments"),
                        ParentComponent = null
                    };

                    instructions.Add(instruction);
                }
            }

            return instructions;
        }

        public static void DeserializeGuids(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object value))
            {
                if (value is string stringValue)
                {
                    // Convert the string to a list of strings
                    List<string> stringList = new List<string> { stringValue };

                    // Replace the string value with the list
                    dict[key] = stringList;

                    // Fix GUID strings in each list item
                    for (int i = 0; i < stringList.Count; i++)
                    {
                        if (!System.Guid.TryParse(stringList[i], out Guid guidValue))
                        {
                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString(stringList[i]);

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }
                    }
                }
                else if (value is List<string> stringList)
                {
                    // Fix GUID strings in each list item
                    for (int i = 0; i < stringList.Count; i++)
                    {
                        if (!System.Guid.TryParse(stringList[i], out Guid guidValue))
                        {
                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString(stringList[i]);

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }
                    }
                }
            }
        }

        private static string FixGuidString(string guidString)
        {
            // Remove any whitespace characters
            guidString = Regex.Replace(guidString, @"\s", "");

            // Attempt to fix common issues with GUID strings
            if (!guidString.StartsWith("{", StringComparison.Ordinal))
                guidString = "{" + guidString;
            if (!guidString.EndsWith("}", StringComparison.Ordinal))
                guidString += "}";
            if (guidString.IndexOf('-') < 0)
                guidString = Regex.Replace(guidString, @"(\w{8})(\w{4})(\w{4})(\w{4})(\w{12})", "$1-$2-$3-$4-$5");


            return guidString;
        }

        public static void DeserializePath(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object pathValue))
            {
                if (pathValue is string path)
                {
                    string formattedPath = FixPathFormatting(path);
                    dict[key] = new List<string> { formattedPath };
                }
                else if (pathValue is IList<string> paths)
                {
                    for (int index = 0; index < paths.Count; index++)
                    {
                        string currentPath = paths[index];
                        string formattedPath = FixPathFormatting(currentPath);
                        paths[index] = formattedPath;
                    }
                }
            }
        }

        public static string FixPathFormatting(string path)
        {
            // Replace backslashes with forward slashes
            string formattedPath = path.Replace('\\', '/');

            // Fix repeated slashes
            formattedPath = Regex.Replace(formattedPath, @"(?<!:)//+", "/");

            // Fix trailing slashes
            formattedPath = formattedPath.TrimEnd('/');

            return formattedPath;
        }

        private static Dictionary<string, object> ConvertTomlTableToDictionary(TomlTable tomlTable)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            foreach (KeyValuePair<string, object> kvp in tomlTable)
            {
                string key = kvp.Key.ToLowerInvariant();
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
                string caseInsensitiveKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (caseInsensitiveKey == null)
                {
                    if (required)
                    {
                        Logger.LogException(new Exception($"Missing or invalid '{key}' field."));
                        return default;
                    }
                    else
                    {
                        return default;
                    }
                }
                value = dict[caseInsensitiveKey];
            }
            if (value is T t)
            {
                return t;
            }

            if (IsListType<T>() && value is IEnumerable enumerable)
            {
                Type elementType = typeof(T).GetGenericArguments()[0];
                dynamic dynamicList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

                foreach (object item in enumerable)
                {
                    dynamic convertedItem = Convert.ChangeType(item, elementType);
                    dynamicList.Add(convertedItem);
                }

                return dynamicList;
            }

            try
            {
                T convertedValue = (T)Convert.ChangeType(value, typeof(T));
                return convertedValue;
            }
            catch (InvalidCastException)
            {
                if (required)
                {
                    throw new ArgumentException($"Invalid '{key}' field type.");
                }
            }
            catch (FormatException)
            {
                if (required)
                {
                    throw new ArgumentException($"Invalid format for '{key}' field.");
                }
            }

            return default;
        }

        private static bool IsListType<T>()
        {
            Type listType = typeof(T);
            return listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>);
        }

        public async Task<(
            bool success,
            Dictionary<FileInfo, SHA1> originalChecksums
        )> ExecuteInstructions(
            IConfirmationDialogCallback confirmDialog,
            List<Component> componentsList
        )
        {
            // Check if we have permission to write to the Destination directory
            if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath)) {
                throw new Exception("Cannot write to the destination directory.");
            }

            async Task<(bool, Dictionary<FileInfo, SHA1>)> ProcessComponentAsync(Component component)
            {
                for (int i1 = 0; i1 < component.Instructions.Count; i1++)
                {
                    Instruction instruction = component.Instructions[i1];
                    //The instruction will run if any of the following conditions are met:
                    //The instruction has no dependencies or restrictions.
                    //The instruction has dependencies, and all of the required components are installed.
                    //The instruction has restrictions, but none of the restricted components are installed.
                    bool shouldRunInstruction = true;
                    if (instruction.Dependencies != null && instruction.Dependencies.Count > 0)
                    {
                        shouldRunInstruction &= instruction.Dependencies.All(requiredGuid =>
                            componentsList.Any(checkComponent => checkComponent.Guid == requiredGuid));
                    }
                    if (instruction.Restrictions != null && instruction.Restrictions.Count > 0)
                    {
                        shouldRunInstruction &= !instruction.Restrictions.Any(restrictedGuid =>
                            componentsList.Any(checkComponent => checkComponent.Guid == restrictedGuid));
                    }

                    if (!shouldRunInstruction)
                    {
                        continue;
                    }

                    // Get the original checksums before making any modifications
                    /*Logger.Log("Calculating game install file hashes");
                    var originalPathsToChecksum = MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories)
                        .ToDictionary(file => file, file => FileChecksumValidator.CalculateSHA1Async(file).Result);

                    if (instruction.OriginalChecksums == null)
                    {
                        instruction.OriginalChecksums = originalPathsToChecksum;
                    }
                    else if (!instruction.OriginalChecksums.SequenceEqual(originalPathsToChecksum))
                    {
                        Logger.Log("Warning! Original checksums of your KOTOR directory do not match the instructions file.");
                    }*/

                    //parse the source/destinations

                    // Enumerate the files/folders with wildcards and add them to the list
                    for (int i = 0; i < instruction.Source.Count; i++)
                    {
                        instruction.Source[i] = Utility.Utility.ReplaceCustomVariables(instruction.Source[i]);
                    }
                    instruction.Source = await Serializer.FileHandler.EnumerateFilesWithWildcards(instruction.Source);
                    if (instruction.Destination != null)
                    {
                        var destinationList = new List<string> { Utility.Utility.ReplaceCustomVariables(instruction.Destination) };
                        instruction.Destination = destinationList.FirstOrDefault();
                    }


                    bool success = false;
                    switch (instruction.Action.ToLower())
                    {
                        case "extract":
                            success = instruction.ExtractFile();
                            break;
                        case "delete":
                            success = await instruction.DeleteFile();
                            break;
                        case "move":
                            success = await instruction.MoveFile();
                            break;
                        case "patch":
                        case "tslpatcher":
                            success = await instruction.ExecuteProgramAsync();
                            success = success && instruction.VerifyInstall();
                            break;
                        case "execute":
                        case "run":
                            success = await instruction.ExecuteProgramAsync();
                            break;
                        case "backup": //todo
                        case "rename": //todo
                        case "dialog": //todo
                        default:
                            // Handle unknown instruction type here
                            Logger.Log($"Unknown instruction {instruction.Action}");
                            success = false;
                            break;
                    }

                    if (!success)
                    {
                        Logger.LogException(new Exception(success.ToString()));
                        Logger.Log($"Instruction {instruction.Action} failed at index {i1}.");
                        bool confirmationResult = await confirmDialog.ShowConfirmationDialog($"Error installing mod {this.Name}, would you like to execute the next instruction anyway?");
                        if (!confirmationResult)
                            return (false, null);
                        else
                            continue;
                    }
                    /*if (instruction.ExpectedChecksums != null)
                    {
                        // Get the new checksums after the modifications
                        var validator = new FileChecksumValidator(
                            destinationPath: MainConfig.DestinationPath.FullName,
                            expectedChecksums: instruction.ExpectedChecksums,
                            originalChecksums: originalPathsToChecksum
                        );
                        bool checksumsMatch = await validator.ValidateChecksumsAsync();
                        if (checksumsMatch)
                        {
                            Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' succeeded and modified files have expected checksums.");
                        }
                        else
                        {
                            Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' succeeded but modified files have unexpected checksums.");
                            bool confirmationResult = await confirmDialog.ShowConfirmationDialog("Warning! Checksums after running install step are not the same as expected. Continue anyway?");
                            if (!confirmationResult)
                            {
                                return (false, originalPathsToChecksum);
                            }
                        }
                    }
                    else
                    {
                        Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' ran, saving the new checksums as expected.");
                        var newChecksums = new Dictionary<FileInfo, System.Security.Cryptography.SHA1>();
                        foreach (FileInfo file in MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories))
                        {
                            System.Security.Cryptography.SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(file);
                            newChecksums[file] = sha1;
                        }
                    }*/
                }
                return (true, new Dictionary<FileInfo, System.Security.Cryptography.SHA1>());
            }

            Task<(bool, Dictionary<FileInfo, System.Security.Cryptography.SHA1>)> result = ProcessComponentAsync(this);
            if (!result.Result.Item1)
            {
                Logger.LogException(new Exception($"Component {this.Name} failed to install the mod correctly with {result}"));
                return (false, null);
            }

            return (true, null);
        }
    }
}
