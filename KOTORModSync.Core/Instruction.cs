// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace KOTORModSync.Core
{
    public class Instruction
    {
        public string Action { get; set; }
        public List<string> Source { get; set; }
        public string Destination { get; set; }
        public List<Guid> Dependencies { get; set; }
        public List<Guid> Restrictions { get; set; }
        public bool Overwrite { get; set; }
        public List<string> Paths { get; set; }
        public string Arguments { get; set; }
        private Component ParentComponent { get; set; }
        public Dictionary<FileInfo, SHA1> ExpectedChecksums { get; set; }
        public Dictionary<FileInfo, SHA1> OriginalChecksums { get; internal set; }

        public static readonly string DefaultInstructions = @"
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
# same as 'run' except it'll try to verify the installation from the TSLPatcher log.

[[thisMod.instructions]]
action = ""TSLPatcher""
source = ""<<modDirectory>>\\path\\to\\mod\\TSLPatcher.exe""
arguments = ""any command line arguments to pass (none available in TSLPatcher)""
";

        public Component ParentComponent1 => ParentComponent;

        public void SetParentComponent(Component parentComponent) => ParentComponent = ParentComponent;

        public static async Task<bool> ExecuteInstructionAsync(Func<Task<bool>> instructionMethod) => await instructionMethod().ConfigureAwait(false);

        // This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
        // This method should not be ran before an instruction is executed.
        // Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
        private (List<string>, DirectoryInfo) ParsePaths()
        {
            List<string> sourcePaths = Source.ConvertAll(Utility.Utility.ReplaceCustomVariables);
            // Enumerate the files/folders with wildcards and add them to the list
            sourcePaths = FileHelper.EnumerateFilesWithWildcards(sourcePaths);
            DirectoryInfo destinationPath = null;
            if (Destination == null)
            {
                return sourcePaths.Count == 0
                    ? throw new Exception($"Could not find any files! Source: {string.Join(", ", Source)}")
                    : ((List<string>, DirectoryInfo))(sourcePaths, destinationPath);
            }

            destinationPath = new DirectoryInfo(
                Utility.Utility.ReplaceCustomVariables(Destination)
                ?? string.Empty
            );
            return sourcePaths.Count == 0
                ? throw new Exception($"Could not find any files! Source: {string.Join(", ", Source)}")
                : ((List<string>, DirectoryInfo))(sourcePaths, destinationPath);
        }

        public async Task<bool> ExtractFileAsync()
        {
            try
            {
                (List<string> sourcePaths, _) = ParsePaths();
                List<Task> extractionTasks = new List<Task>(1024);

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

                            if (!ArchiveHelper.IsArchive(thisFile.Extension))
                            {
                                var ex = new ArgumentNullException($"{ParentComponent.Name} failed to extract file {thisFile}. Invalid archive?");
                                Logger.LogException(ex);
                                throw ex;
                            }

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

                                if (archive == null)
                                    throw new InvalidOperationException("Unable to parse archive '{sourcePath}'");

                                IReader reader = archive.ExtractAllEntries();
                                while (reader.MoveToNextEntry())
                                {
                                    if (reader.Entry.IsDirectory)
                                    {
                                        continue;
                                    }

                                    string destinationFolder = Path.GetFileNameWithoutExtension(thisFile.Name);
                                    if (thisFile.Directory == null) { continue; }

                                    string destinationPath = Path.Combine(thisFile.Directory.FullName, destinationFolder, reader.Entry.Key);
                                    string destinationDirectory = Path.GetDirectoryName(destinationPath);

                                    Logger.Log($"Extract {reader.Entry.Key} to {thisFile.Directory.FullName}");

                                    if (!Directory.Exists(destinationDirectory))
                                    {
                                        Logger.Log($"Create directory {destinationDirectory}");
                                        _ = Directory.CreateDirectory(destinationDirectory ?? string.Empty);
                                    }

                                    await Task.Run(() => reader.WriteEntryToDirectory(destinationDirectory ?? string.Empty, new ExtractionOptions { ExtractFullPath = false, Overwrite = true }));
                                }
                            }
                        }
                        finally
                        {
                            _ = semaphore.Release(); // Release the semaphore slot
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

        public bool DeleteFile()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo destinationPath) = ParsePaths();

                var deleteTasks = new List<Task>(65535);

                foreach (FileInfo thisFile in sourcePaths.Select(t => new FileInfo(t)))
                {
                    if (!Path.IsPathRooted(thisFile.FullName))
                    {
                        var ex = new ArgumentException(
                            $"Invalid wildcards/not a valid path: {thisFile.FullName}");
                        Logger.LogException(ex);
                        return false;
                    }

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

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }

        public bool MoveFile()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo destinationPath) = ParsePaths();
                foreach (string sourcePath in sourcePaths)
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string destinationFilePath = Path.Combine(destinationPath.FullName, fileName);

                    // Check if the destination file already exists
                    if (!Overwrite && File.Exists(destinationFilePath))
                    {
                        if (!Overwrite)
                        {
                            Logger.Log(
                                $"Skipping file {Path.GetFileName(destinationFilePath)} (Overwrite is false)");
                        }

                        continue;
                    }

                    // Check if the destination file already exists
                    if (File.Exists(destinationFilePath))
                    {
                        Logger.Log(
                            $"File already exists, deleting existing file {destinationFilePath}");
                        // Delete the existing file
                        File.Delete(destinationFilePath);
                    }

                    // Move the file
                    Logger.Log(
                        $"Move '{Path.GetFileName(sourcePath)}' to '{destinationFilePath}'");

                    File.Move(sourcePath, destinationFilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }

        public async Task<bool> ExecuteTSLPatcherAsync()
        {
            try
            {
                (List<string> sourcePaths, DirectoryInfo _) = ParsePaths();
                bool isSuccess = false; // Track the success status
                foreach (string t in sourcePaths)
                {
                    var thisProgram = new FileInfo(t);
                    if (!thisProgram.Exists)
                        throw new FileNotFoundException($"The file {t} could not be located on the disk");

                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3600)); // cancel if running longer than 30 minutes.

                    // arg1 = swkotor directory
                    // arg2 = mod directory (where TSLPatcher lives)
                    // arg3 = (optional) install option index
                    string args = $"\"{MainConfig.DestinationPath}\" \"{Directory.GetParent(thisProgram.FullName)}\" \"\"";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        CreateNoWindow = false,
                        FileName = Path.Combine(FileHelper.ResourcesDirectory, "TSLPatcherCLI.exe"),
                        WindowStyle = ProcessWindowStyle.Normal,
                        Arguments = args
                    };

                    var exeProcess = Process.Start(startInfo);
                    Task<string> outputTask = exeProcess.StandardOutput.ReadToEndAsync();

                    while (!exeProcess.HasExited && !cancellationTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationTokenSource.Token);
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
                        return false;
                    }

                    isSuccess = true;
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
                (List<string> sourcePaths, _) = ParsePaths();
                bool isSuccess = true; // Track the success status

                foreach (string sourcePath in sourcePaths)
                {
                    if (Action == "TSLPatcher")
                        FileHelper.ReplaceLookupGameFolder(new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? string.Empty));

                    var thisProgram = new FileInfo(sourcePath);
                    if (!thisProgram.Exists)
                        throw new FileNotFoundException($"The file {sourcePath} could not be located on the disk");

                    try
                    {
                        if (await PlatformAgnosticMethods.ExecuteProcessAsync(thisProgram, ""))
                            continue;

                        isSuccess = false;
                        break;
                    }
                    catch (Exception ex)
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

        public bool VerifyInstall()
        {
            bool errors = false;
            bool found = false;
            (List<string> sourcePaths, DirectoryInfo _) = ParsePaths();
            foreach (string installLogFile in sourcePaths.Select(sourcePath => System.IO.Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, "install.rtf")))
            {
                if (!File.Exists(installLogFile))
                {
                    Logger.Log("Install log file not found.");
                    continue;
                }

                found = true;
                string installLogContent = File.ReadAllText(installLogFile);
                string[] bulletPoints = installLogContent.Split('\u2022');

                foreach (string bulletPoint in bulletPoints
                    .Where(bulletPoint => bulletPoint.Contains("Error")))
                {
                    Logger.Log($"Install log contains warning or error message: {bulletPoint.Trim()}");
                    errors = true;
                }
            }

            return found && !errors;
        }
    }
}