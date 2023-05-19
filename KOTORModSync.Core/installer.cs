// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Tomlyn.Model;
using static KOTORModSync.Core.Utility.Utility;

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
        public Dictionary<FileInfo, System.Security.Cryptography.SHA1> ExpectedChecksums { get; set; }

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
    paths = [""<<modDirectory>>\\path\\to\\mod\\TSLPatcher.exe""]
    arguments = ""any command line arguments to pass (none available in TSLPatcher)""
";

        public static async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod) => await instructionMethod().ConfigureAwait(false);

        public async Task<bool> ExtractFile()
        {
            try
            {
                var extractTasks = new List<Task>();
                SemaphoreSlim semaphore = new SemaphoreSlim(Utility.PlatformAgnosticMethods.CalculateMaxDegreeOfParallelism()); // Set the maximum degree of parallelism

                foreach (string fileStrPath in this.Source)
                {
                    var thisFile = new FileInfo(fileStrPath);
                    if (!thisFile.Exists)
                    {
                        Exception ex = new FileNotFoundException($"The file {fileStrPath} could not be located on the disk");
                        Logger.LogException(ex);
                        throw ex;
                    }

                    using (Stream stream = File.OpenRead(thisFile.FullName))
                    {
                        IArchive archive = null;

                        if (RarArchive.IsRarFile(stream))
                        {
                            archive = RarArchive.Open(stream);
                        }
                        else if (ZipArchive.IsZipFile(stream))
                        {
                            archive = ZipArchive.Open(stream);
                        }
                        else if (SevenZipArchive.IsSevenZipFile(stream))
                        {
                            archive = SevenZipArchive.Open(stream);
                        }

                        if (archive != null)
                        {
                            foreach (IArchiveEntry entry in archive.Entries)
                            {
                                if (!entry.IsDirectory)
                                {
                                    Task extractTask = Task.Run(async () =>
                                    {
                                        await semaphore.WaitAsync(); // Acquire a semaphore slot

                                        try
                                        {
                                            entry.WriteToDirectory(this.ParentComponent.tempPath.FullName, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                                        }
                                        finally
                                        {
                                            semaphore.Release(); // Release the semaphore slot
                                        }
                                    });

                                    extractTasks.Add(extractTask);
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
                }

                // Wait for all extract tasks to complete
                await Task.WhenAll(extractTasks);

                return true; // Extraction succeeded
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occurred during extraction
                Logger.Log($"Error occurred during file extraction: {ex.Message}");
                return false; // Extraction failed
            }
        }


        public async Task<bool> DeleteFile()
        {
            try
            {
                var deleteTasks = new List<Task>();

                // Enumerate the files/folders with wildcards and add them to the list
                var filesToDelete = await Serializer.FileHandler.EnumerateFilesWithWildcards(this.Source);

                SemaphoreSlim semaphore = new SemaphoreSlim(Utility.PlatformAgnosticMethods.CalculateMaxDegreeOfParallelism()); // Set the maximum degree of parallelism

                foreach (string fileToDelete in filesToDelete)
                {
                    if (Path.IsPathRooted(fileToDelete))
                    {
                        var thisFile = new FileInfo(fileToDelete);
                        if (!thisFile.Exists)
                        {
                            Exception ex = new FileNotFoundException($"The file {fileToDelete} could not be located on the disk");
                            Logger.LogException(ex);
                            return false;
                        }

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
                        var ex = new ArgumentException($"Invalid wildcards/not a valid path: {fileToDelete}");
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

                SemaphoreSlim semaphore = new SemaphoreSlim(Utility.PlatformAgnosticMethods.CalculateMaxDegreeOfParallelism()); // Set the maximum degree of parallelism

                foreach (string fileStrPath in this.Source) {
                    var thisFile = new FileInfo(fileStrPath);
                    if (!thisFile.Exists) {
                        Exception ex = new FileNotFoundException($"The file {fileStrPath} could not be located on the disk");
                        Logger.LogException(ex);
                        return false;
                    }

                    string destinationPath = Path.Combine(this.Destination, thisFile.Name);

                    // Check if the destination file already exists
                    if (this.Overwrite || !File.Exists(destinationPath)) {
                        // Move the file asynchronously
                        Task moveTask = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync(); // Acquire a semaphore slot

                            try {
                                await Serializer.FileHandler.MoveFileAsync(thisFile.FullName, destinationPath);
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


        public async Task<bool> ExecuteTSLPatcherAsync()
        {
            try
            {
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3600)); // cancel if running longer than 30 minutes.
                string path = Paths[0];
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
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
                    throw new Exception("TSLPatcher timed out after 30 minutes.");
                }

                string output = await outputTask;

                return process.ExitCode != 0
                    ? throw new Exception($"TSLPatcher failed with exit code {process.ExitCode}. Output:\n{output}")
                    : !VerifyInstall() ? throw new Exception("TSLPatcher failed to install the mod correctly.") : true;
            } catch (Exception ex) {
                Logger.LogException(ex);
                return false;
            }


        }

        public bool VerifyInstall()
        {
            // Verify if the destination directory has been modified
            /*DateTime destinationDirectoryLastModified = Directory.GetLastWriteTime(MainConfig.DestinationPath.FullName);

            if (destinationDirectoryLastModified < component.SourceLastModified)
            {
                Logger.Log("Destination directory has not been modified.");
                return false;
            }*/

            // Verify if any error or warning message is present in the install.rtf file
            string installLogFile = System.IO.Path.Combine(MainConfig.DestinationPath.FullName, "install.rtf");

            if (!File.Exists(installLogFile))
            {
                Logger.Log("Install log file not found.");
                return false;
            }

            string installLogContent = File.ReadAllText(installLogFile);
            string[] bulletPoints = installLogContent.Split('\u2022');

            foreach (string bulletPoint in bulletPoints)
            {
                if (bulletPoint.Contains("Warning") || bulletPoint.Contains("Error"))
                {
                    Logger.Log($"Install log contains warning or error message: {bulletPoint.Trim()}");
                    return false;
                }
            }

            return true;
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
        public List<string> Paths { get; set; }
        public List<Instruction> Instructions { get; set; }
        public DateTime SourceLastModified { get; internal set; }

        public static string defaultComponent = @"
[[thisMod]]
    name = ""your custom name of your mod""
    guid = ""{B3525945-BDBD-45D8-A324-AAF328A5E13E}""
    # Copy and paste any guid of any mod you depend on here, format like below
    dependencies = [
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
        ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
    ]
    # Copy and paste any guid of any incompatible mod here, format like below
    restrictions = [
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
        ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
    ]
    installOrder = 3
    # You can specify multiple paths to the same mod, but it's easier to use just one path and handle everything in instructions.
    paths = [
        ""<<modDirectory>>\\path\\to\\mod_location2"",
        ""<<modDirectory>>\\path\\to\\mod_location1""
    ]";

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
                path = NormalizeAndReplacePath(path);
                paths.Add(path);
            }
            NormalizeAndReplacePath(componentDict, "paths");
            if (componentDict.TryGetValue("paths", out object pathsValue) && pathsValue is IList<string> pathsList)
            {
                paths.AddRange(pathsList);
            }

            this.Name = GetRequiredValue<string>(componentDict, "name");
            this.Guid = GetRequiredValue<string>(componentDict, "guid");
            this.InstallOrder = GetValueOrDefault<int>(componentDict, "installorder");
            this.Dependencies = GetValueOrDefault<List<string>>(componentDict, "dependencies");
            this.Instructions = DeserializeInstructions(GetValueOrDefault<TomlTableArray>(componentDict, "instructions"));
            this.Paths = GetValueOrDefault<List<string>>(componentDict, "Paths");
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

                    NormalizeAndReplacePath(instructionDict, "source");
                    NormalizeAndReplacePath(instructionDict, "destination");
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

        // Function to normalize and replace paths
        public static void NormalizeAndReplacePath(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object pathValue) && pathValue is IList<string> paths)
            {
                for (int index = 0; index < paths.Count; index++)
                {
                    string thisPath = paths[index];
                    thisPath = thisPath.Replace("<<modDirectory>>", MainConfig.ModConfigPath.FullName);
                    thisPath = thisPath.Replace("<<kotorDirectory>>", MainConfig.DestinationPath.FullName);
                    if (thisPath != paths[index])
                        thisPath = Path.Combine(MainConfig.ModConfigPath.FullName, thisPath);
                    paths[index] = Path.GetFullPath(thisPath);
                }
            }
        }

        // Function to normalize and replace paths
        public static string NormalizeAndReplacePath(string path)
        {
            string normalizedPath = path.Replace("<<modDirectory>>", MainConfig.ModConfigPath.FullName);
            normalizedPath = normalizedPath.Replace("<<kotorDirectory>>", MainConfig.DestinationPath.FullName);
            return Path.GetFullPath(normalizedPath);
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

        public static async Task<(bool success, Dictionary<FileInfo, System.Security.Cryptography.SHA1> originalChecksums)> ExecuteInstructions(IConfirmationDialogCallback confirmDialog, List<Component> componentsList)
        {

            // Check if we have permission to write to the Destination directory
            if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath)) {
                throw new Exception("Cannot write to the destination directory.");
            }

            async Task<(bool, Dictionary<FileInfo, System.Security.Cryptography.SHA1>)> ProcessComponentAsync(Component component)
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

                    // there's no real reason to run these async except to keep the UI from freezing up during a task.
                    switch (instruction.Action.ToLower())
                    {
                        case "extract":
                            success = await instruction.ExtractFile();
                            break;

                        case "delete":
                            success = await instruction.DeleteFile();
                            break;

                        case "move":
                            success = await instruction.MoveFile();
                            break;
                        case "patch":
                        case "execute":
                        case "tslpatcher":
                            success = await instruction.ExecuteTSLPatcherAsync();
                            break;
                        case "backup": //todo
                        case "rename": //todo
                        default:
                            // Handle unknown instruction type here
                            break;
                    }

                    if (!success)
                    {
                        Logger.LogException(new Exception(success.ToString()));
                        Logger.Log($"Instruction {instruction.Action} failed to install the mod correctly.");
                        return (false, null);
                    }
                    if (instruction.ExpectedChecksums != null)
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
                    }
                }
                return (true, new Dictionary<FileInfo, System.Security.Cryptography.SHA1>());
            }

            string modConfigFile = Path.Combine(MainConfig.ModConfigPath.FullName, "modpack.toml");
            List<Component> components = Utility.Serializer.FileHandler.ReadComponentsFromFile(modConfigFile);

            foreach (Component component in components)
            {
                Task<(bool, Dictionary<FileInfo, System.Security.Cryptography.SHA1>)> result = ProcessComponentAsync(component);
                if (!result.Result.Item1)
                {
                    Logger.LogException(new Exception($"Component {component.Name} failed to install the mod correctly with {result}"));
                    bool confirmationResult = await confirmDialog.ShowConfirmationDialog($"Error installing mod {component.Name}, continue with install anyway?");
                    if (!confirmationResult)
                    {
                        return (false, null);
                    }
                }
            }

            return (true, null);
        }
    }
}
