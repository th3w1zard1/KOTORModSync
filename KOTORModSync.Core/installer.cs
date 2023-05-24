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
using SharpCompress.Readers;
using System.Reflection;

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
source = [
    ""<<modDirectory>>\\path\\to\\mod\\file1.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file2.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file3.tpc""
]
dependencies = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""

[[thisMod.instructions]]
action = ""move""
source = [
    ""<<modDirectory>>\\path\\to\\mod\\file1.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file2.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file3.tpc""
]
destination = ""<<kotorDirectory>>\\Override""
overwrite = ""True""
restrictions = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""

[[thisMod.instructions]]
action = ""run""
Source = [""<<modDirectory>>\\path\\to\\mod\\program.exe""]
arguments = ""any command line arguments to pass""
# same as 'run' except it'll try to verify the installation from the tslpatcher log.

[[thisMod.instructions]]
action = ""tslpatcher""
source = ""<<modDirectory>>\\path\\to\\mod\\TSLPatcher.exe""
arguments = ""any command line arguments to pass (none available in TSLPatcher)""
";

        public static async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod) => await instructionMethod().ConfigureAwait(false);


        private async Task<(List<string>, DirectoryInfo)> ParsePathsAsync()
        {
            var sourcePaths = new List<string>();
            // Enumerate the files/folders with wildcards and add them to the list
            for (int i = 0; i < this.Source.Count; i++)
            {
                sourcePaths.Add(Utility.Utility.ReplaceCustomVariables(this.Source[i]));
            }
            sourcePaths = await FileHandler.EnumerateFilesWithWildcards(sourcePaths);
            DirectoryInfo destinationPath = null;
            if (this.Destination != null)
            {
                destinationPath = new DirectoryInfo(Utility.Utility.ReplaceCustomVariables(this.Destination));
            }
            return (sourcePaths, destinationPath);
        }

        public async Task<bool> ExtractFileAsync()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo _) = await ParsePathsAsync();
                List<Task> extractionTasks = new List<Task>();

                // Use SemaphoreSlim to limit concurrent extractions
                SemaphoreSlim semaphore = new SemaphoreSlim(5); // Limiting to 5 concurrent extractions

                foreach (string sourcePath in sourcePaths)
                {
                    await semaphore.WaitAsync(); // Acquire a semaphore slot

                    extractionTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var thisFile = new FileInfo(sourcePath);
                            Logger.Log($"File path: {thisFile.FullName}");

                            if (ArchiveHelper.IsArchive(thisFile.Extension))
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

                                    if (archive != null)
                                    {
                                        var reader = archive.ExtractAllEntries();
                                        while (reader.MoveToNextEntry())
                                        {
                                            if (!reader.Entry.IsDirectory)
                                            {
                                                string destinationFolder = Path.GetFileNameWithoutExtension(thisFile.Name);
                                                string destinationPath = Path.Combine(thisFile.Directory.FullName, destinationFolder, reader.Entry.Key);
                                                string destinationDirectory = Path.GetDirectoryName(destinationPath);

                                                Logger.Log($"Extract {reader.Entry.Key} to {thisFile.Directory.FullName}");

                                                if (!Directory.Exists(destinationDirectory))
                                                {
                                                    Logger.Log($"Create directory {destinationDirectory}");
                                                    Directory.CreateDirectory(destinationDirectory);
                                                }

                                                await Task.Run(() =>
                                                {
                                                    reader.WriteEntryToDirectory(destinationDirectory, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                                                });
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
                        finally
                        {
                            semaphore.Release(); // Release the semaphore slot
                        }
                    }));
                }

                await Task.WhenAll(extractionTasks); // Wait for all extraction tasks to complete

                return true; // Extraction succeeded
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occurred during extraction
                Logger.LogException(ex);
                return false; // Extraction failed
            }
        }




        public async Task<bool> DeleteFileAsync()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo destinationPath) = await ParsePathsAsync();

                var deleteTasks = new List<Task>();

                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    var thisFile = new FileInfo(sourcePaths[i]);

                    if (Path.IsPathRooted(thisFile.FullName))
                    {
                        // Delete the file synchronously
                        try
                        {
                            File.Delete(thisFile.FullName);
                            Logger.Log($"Deleting {thisFile.FullName}...");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex);
                            return false;
                        }
                    }
                    else
                    {
                        var ex = new ArgumentException($"Invalid wildcards/not a valid path: {thisFile.FullName}");
                        Logger.LogException(ex);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }



        public async Task<bool> MoveFileAsync()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo destinationPath) = await ParsePathsAsync();

                foreach (string sourcePath in sourcePaths)
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string destinationFilePath = Path.Combine(destinationPath.FullName, fileName);

                    // Check if the destination file already exists
                    if (Overwrite || !File.Exists(destinationFilePath))
                    {
                        // Check if the destination file already exists
                        if (File.Exists(destinationFilePath))
                        {
                            Logger.Log($"File already exists, deleting existing file {destinationFilePath}");
                            // Delete the existing file
                            File.Delete(destinationFilePath);
                        } else {
                            Logger.Log($"File already exists, but overwrite is false. Skipping file {destinationFilePath}");
                            continue;
                        }

                        // Move the file
                        Logger.Log($"Moving {sourcePath} to {destinationFilePath}... Overwriting? {Overwrite}");
                        File.Move(sourcePath, destinationFilePath);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }

        public static string getResourcesDirectory()
        {
            string outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(outputDirectory, "Resources");
        }

        public async Task<bool> ExecuteTSLPatcherAsync()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo _) = await ParsePathsAsync();
                bool isSuccess = false; // Track the success status
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    var thisProgram = new FileInfo(sourcePaths[i]);
                    if (!thisProgram.Exists)
                    {
                        throw new FileNotFoundException($"The file {sourcePaths[i]} could not be located on the disk");
                    }
                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3600)); // cancel if running longer than 30 minutes.
                                                                                     // arg1 = swkotor directory
                                                                                     // arg2 = mod directory (where TSLPatcher lives)
                                                                                     // arg3 = (optional) install option index
                    string args = $"\"{MainConfig.DestinationPath}\" \"{Directory.GetParent(thisProgram.FullName)}\" \"\"";

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.CreateNoWindow = false;
                    startInfo.FileName = Path.Combine(getResourcesDirectory(), "TSLPatcherCLI.exe");
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                    startInfo.Arguments = args;

                    var exeProcess = Process.Start(startInfo);
                    Task<string> outputTask = exeProcess.StandardOutput.ReadToEndAsync();

                    while (!exeProcess.HasExited && !cancellationTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }
                    if (!exeProcess.HasExited)
                    {
                        exeProcess.Kill();
                        throw new TimeoutException("Process timed out after 30 minutes.");
                    }

                    string output = await outputTask;

                    if (exeProcess.ExitCode != 0)
                    {
                        Logger.Log($"Process failed with exit code {exeProcess.ExitCode}. Output:\n{output}");
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

        public async Task<bool> ExecuteProgramAsync()
        {
            try
            {
                (List<string> sourcePaths, _) = await ParsePathsAsync();
                bool isSuccess = true; // Track the success status

                foreach (string sourcePath in sourcePaths)
                {
                    if (Action == "TSLPatcher")
                    {
                        FileHandler.ReplaceLookupGameFolder(new DirectoryInfo(Path.GetDirectoryName(sourcePath)));
                    }

                    var thisProgram = new FileInfo(sourcePath);
                    if (!thisProgram.Exists)
                    {
                        throw new FileNotFoundException($"The file {sourcePath} could not be located on the disk");
                    }
                    try
                    {
                        if (!await ExecuteProcessAsync(thisProgram))
                        {
                            isSuccess = false;
                            break;
                        }
                    }
                    catch(Exception ex)
                    {
                        Logger.LogException(ex);
                        return false;
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

        private async Task<bool> ExecuteProcessAsync(FileInfo programFile)
        {
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3600)))
            {
                using (var process = new Process())
                {
                    var startInfo = process.StartInfo;
                    startInfo.FileName = programFile.FullName;
                    startInfo.Arguments = Arguments;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;

                    try
                    {
                        if (!process.Start())
                        {
                            throw new InvalidOperationException("Failed to start the process.");
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        startInfo.UseShellExecute = true;
                        startInfo.RedirectStandardOutput = false;
                        startInfo.RedirectStandardError = false;

                        if (!process.Start())
                        {
                            throw new InvalidOperationException("Failed to start the process.");
                        }
                    }

                    await Task.Run(() => process.WaitForExit(), cancellationTokenSource.Token);

                    if (!process.HasExited)
                    {
                        process.Kill();
                        throw new TimeoutException("Process timed out after 30 minutes.");
                    }

                    string output = null;
                    string error = null;
                    if (startInfo.RedirectStandardOutput)
                    {
                        output = await process.StandardOutput.ReadToEndAsync();
                        error = await process.StandardError.ReadToEndAsync();
                    }

                    if (process.ExitCode != 0)
                    {
                        Logger.Log($"Process failed with exit code {process.ExitCode}. Output:\n{output}");
                        return false;
                    }

                    Logger.Log($"Output: {output}\n Error: {error}\n");
                    return true;
                }
            }
        }




        public async Task<bool> VerifyInstallAsync()
        {
            bool errors = false;
            bool found = false;
            (List<string> sourcePaths, DirectoryInfo _) = await ParsePathsAsync();
            foreach (string sourcePath in sourcePaths)
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

                    bool success = false;
                    switch (instruction.Action.ToLower())
                    {
                        case "extract":
                            success = await instruction.ExtractFileAsync();
                            break;
                        case "delete":
                            success = await instruction.DeleteFileAsync();
                            break;
                        case "move":
                            success = await instruction.MoveFileAsync();
                            break;
                        case "patch":
                        case "tslpatcher":
                            //success = await instruction.ExecuteTSLPatcherAsync();
                            success = await instruction.ExecuteProgramAsync();
                            //success = success && await instruction.VerifyInstallAsync();
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
                return (true, new Dictionary<FileInfo, SHA1>());
            }

            (bool, Dictionary<FileInfo, SHA1>) result = await ProcessComponentAsync(this);
            if (!result.Item1)
            {
                Logger.LogException(new Exception($"Component {this.Name} failed to install the mod correctly with {result}"));
                return (false, null);
            }

            return (true, null);
        }
    }
}
