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
        public Guid Guid { get; set; }
        public List<string> Source { get; set; }
        private List<string> sourcePaths { get; set; }
        public string Destination { get; set; }
        private DirectoryInfo destinationPath { get; set; }
        public List<Guid> Dependencies { get; set; }
        public List<Guid> Restrictions { get; set; }
        public bool Overwrite { get; set; }
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
dependencies = """"
overwrite = true

[[thisMod.instructions]]
action = ""move""
source = [
    ""<<modDirectory>>\\path\\to\\mod\\file1.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file2.tpc"",
    ""<<modDirectory>>\\path\\to\\mod\\file3.tpc""
]
destination = ""<<kotorDirectory>>\\Override""
overwrite = ""True""
restrictions = """"

[[thisMod.instructions]]
action = ""run""
Source = [""<<modDirectory>>\\path\\to\\mod\\program.exe""]
arguments = ""any command line arguments to pass""
[[thisMod.instructions]]
action = ""TSLPatcher""
source = ""<<modDirectory>>\\path\\to\\mod\\TSLPatcher directory""
arguments = ""any command line arguments to pass (in TSLPatcher, this is the index of the desired option in namespaces.ini))""
";

        public void SetParentComponent( Component parentComponent ) =>
            ParentComponent = parentComponent;

        public static async Task<bool> ExecuteInstructionAsync(
            Func<Task<bool>> instructionMethod
        ) =>
            await instructionMethod().ConfigureAwait( false );

        // This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
        // This method should not be ran before an instruction is executed.
        // Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
        // ^ perhaps the above is user error though? Either way, we attempt to baby them here.
        public void SetRealPaths( bool noValidate = false )
        {
            sourcePaths
                = Source.ConvertAll( Utility.Utility.ReplaceCustomVariables );
            // Enumerate the files/folders with wildcards and add them to the list
            sourcePaths = FileHelper.EnumerateFilesWithWildcards( sourcePaths );

            if ( Destination == null )
            {
                if ( !noValidate && sourcePaths.Count == 0 )
                {
                    throw new Exception(
                        $"Could not find any files! Source: {string.Join( ", ", Source )}" );
                }

                return;
            }

            destinationPath = new DirectoryInfo(
                Utility.Utility.ReplaceCustomVariables( Destination )
                ?? throw new InvalidOperationException( "No destination set!" ) );

            if ( !noValidate && sourcePaths.Count == 0 )
            {
                throw new Exception(
                    $"Could not find any files! Source: {string.Join( ", ", Source )}" );
            }

            return;
        }

        public async Task<bool> ExtractFileAsync(
            Utility.Utility.IConfirmationDialogCallback confirmDialog
        )
        {
            try
            {
                var extractionTasks = new List<Task>( 25 );

                // Use SemaphoreSlim to limit concurrent extractions
                var
                    semaphore = new SemaphoreSlim( 5 ); // Limiting to 5 concurrent extractions
                bool success = true;
                foreach ( string sourcePath in sourcePaths )
                {
                    await semaphore.WaitAsync(); // Acquire a semaphore slot

                    extractionTasks.Add(
                        Task.Run(
                            async () =>
                            {
                                try
                                {
                                    var thisFile = new FileInfo( sourcePath );
                                    Logger.Log( $"File path: {thisFile.FullName}" );

                                    if ( !ArchiveHelper.IsArchive( thisFile.Extension ) )
                                    {
                                        Logger.Log( $"[Error] {ParentComponent.Name} failed to extract file '{thisFile.Name}'. Invalid archive?" );
                                        success = false;
                                        return;
                                    }

                                    using ( FileStream stream = File.OpenRead( thisFile.FullName ) )
                                    {
                                        IArchive archive = ArchiveHelper.OpenArchive(
                                            stream,
                                            thisFile.FullName
                                        );

                                        if ( archive == null )
                                        {
                                            Logger.LogException(
                                                new InvalidOperationException(
                                                    $"Unable to parse archive '{sourcePath}'" )
                                            );
                                            success = false;
                                            return;
                                        }

                                        IReader reader = archive.ExtractAllEntries();
                                        while ( reader.MoveToNextEntry() )
                                        {
                                            if ( reader.Entry.IsDirectory ) continue;
                                            if ( thisFile.Directory?.FullName == null ) continue;

                                            string destinationFolder = Path.GetFileNameWithoutExtension( thisFile.Name );
                                            string destinationItemPath = Path.Combine(
                                                thisFile.Directory.FullName,
                                                destinationFolder,
                                                reader.Entry.Key
                                            );
                                            string destinationDirectory = Path.GetDirectoryName( destinationItemPath );

                                            if ( destinationDirectory != null && !Directory.Exists( destinationDirectory ) )
                                            {
                                                Logger.Log( $"Create directory {destinationDirectory}" );
                                                _ = Directory.CreateDirectory( destinationDirectory );
                                            }

                                            Logger.Log( $"Extract {reader.Entry.Key} to {thisFile.Directory.FullName}" );

                                            try
                                            {
                                                await Task.Run(
                                                    () => reader.WriteEntryToDirectory(
                                                        destinationDirectory ?? throw new InvalidOperationException(),
                                                        ArchiveHelper.DefaultExtractionOptions
                                                    )
                                                );
                                            }
                                            catch ( Exception )
                                            {
                                                await Task.Run(
                                                    () => Logger.Log( $"[Warning] Skipping file '{reader.Entry.Key}' due to lack of permissions." )
                                                );
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    _ = semaphore.Release(); // Release the semaphore slot
                                }
                            } ) );
                }

                await Task.WhenAll(
                    extractionTasks ); // Wait for all extraction tasks to complete

                return success; // Extraction succeeded
            }
            catch ( Exception ex )
            {
                // Handle any exceptions that occurred during extraction
                Logger.LogException( ex );
                return false; // Extraction failed
            }
        }

        public static void DeleteDuplicateFile(
            string directoryPath,
            string fileExtension,
            Utility.Utility.IConfirmationDialogCallback confirmDialog
        )
        {
            if ( string.IsNullOrEmpty( directoryPath )
                || !Directory.Exists( directoryPath )
                || !Utility.Utility.IsDirectoryWritable( new DirectoryInfo( directoryPath ) )
                )
            {
                throw new ArgumentException( "Invalid or inaccessible directory path." );
            }

            string[] files = Directory.GetFiles( directoryPath );
            var fileNameCounts = new Dictionary<string, int>();

            foreach ( string filePath in files )
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( filePath );

                if ( string.IsNullOrEmpty( fileNameWithoutExtension ) ) continue;

                if ( fileNameCounts.TryGetValue( fileNameWithoutExtension, out int count ) )
                {
                    fileNameCounts[fileNameWithoutExtension] = count + 1;
                    continue;
                }

                fileNameCounts[fileNameWithoutExtension] = 1;
            }

            foreach ( string filePath in files )
            {
                string fileName = Path.GetFileName( filePath );
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( filePath );
                string fileExtensionFromFile = Path.GetExtension( filePath );

                if ( string.IsNullOrEmpty( fileNameWithoutExtension )
                    || !fileNameCounts.ContainsKey( fileNameWithoutExtension )
                    || fileNameCounts[fileNameWithoutExtension] <= 1 || !string.Equals(
                        fileExtensionFromFile,
                        fileExtension,
                        StringComparison.OrdinalIgnoreCase ) ) { continue; }

                try
                {
                    File.Delete( filePath );
                    Logger.Log( $"Deleted file: {fileName}" );
                }
                catch ( Exception ex )
                {
                    Logger.LogException( ex );
                    // Decide whether to throw or handle the exception here
                }
            }
        }

        public bool DeleteFile( Utility.Utility.IConfirmationDialogCallback confirmDialog )
        {
            try
            {
                foreach ( string thisFilePath in sourcePaths )
                {
                    var thisFile = new FileInfo( thisFilePath );

                    if ( !Path.IsPathRooted( thisFilePath ) || !thisFile.Exists )
                    {
                        var ex = new ArgumentNullException(
                            $"Invalid wildcards or file does not exist: {thisFilePath}" );
                        Logger.LogException( ex );
                        return false;
                    }

                    // Delete the file synchronously
                    try
                    {
                        File.Delete( thisFilePath );
                        Logger.Log( $"Deleting {thisFilePath}..." );
                    }
                    catch ( Exception ex )
                    {
                        Logger.LogException( ex );
                        return false;
                    }
                }

                return true;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        public bool RenameFile( Utility.Utility.IConfirmationDialogCallback confirmDialog )
        {
            try
            {
                bool success = true;
                foreach ( string sourcePath in Source.ConvertAll(
                    Utility.Utility.ReplaceCustomVariables ) )
                {
                    // Check if the source file already exists
                    string fileName = Path.GetFileName( sourcePath );
                    if ( !File.Exists( sourcePath ) )
                    {
                        Logger.Log( $"{fileName} does not exist!" );
                        success = false;
                        continue;
                    }

                    // Check if the destination file already exists
                    string destinationFilePath = Path.Combine(
                        Path.GetDirectoryName( sourcePath ) ?? string.Empty,
                        Destination );
                    if ( File.Exists( destinationFilePath ) )
                    {
                        if ( Overwrite )
                        {
                            Logger.Log( $"Replacing {destinationFilePath}" );
                            File.Delete( destinationFilePath );
                        }
                        else
                        {
                            success = false;
                            Logger.LogException(
                                new InvalidOperationException(
                                    $"Skipping file {sourcePath} (A file with the name {Path.GetFileName( destinationFilePath )} already exists)" ) );
                            continue;
                        }
                    }

                    // Move the file
                    try
                    {
                        Logger.Log( $"Rename '{fileName}' to '{destinationFilePath}'" );
                        File.Move( sourcePath, destinationFilePath );
                    }
                    catch ( IOException ex )
                    {
                        // Handle file move error, such as destination file already exists
                        success = false;
                        Logger.LogException( ex );
                    }
                }

                return success;
            }
            catch ( Exception ex )
            {
                // Handle any unexpected exceptions
                Logger.LogException( ex );
                return false;
            }
        }

        public bool CopyFile( Utility.Utility.IConfirmationDialogCallback confirmDialog )
        {
            try
            {
                foreach ( string sourcePath in sourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string destinationFilePath = Path.Combine(
                        destinationPath.FullName,
                        fileName );

                    // Check if the destination file already exists
                    if ( !Overwrite && File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"Skipping file {Path.GetFileName( destinationFilePath )} (Overwrite is false)"
                        );
                        continue;
                    }

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"File already exists, deleting existing file {destinationFilePath}" );
                        // Delete the existing file
                        File.Delete( destinationFilePath );
                    }

                    // Copy the file
                    Logger.Log(
                        $"Copy '{Path.GetFileName( sourcePath )}' to '{destinationFilePath}'" );

                    File.Copy( sourcePath, destinationFilePath );
                }

                return true;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        public bool MoveFile( Utility.Utility.IConfirmationDialogCallback confirmDialog )
        {
            try
            {
                foreach ( string sourcePath in sourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string destinationFilePath = Path.Combine(
                        destinationPath.FullName,
                        fileName );

                    // Check if the destination file already exists
                    if ( !Overwrite && File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"Skipping file {Path.GetFileName( destinationFilePath )} (Overwrite is false)" );

                        continue;
                    }

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"File already exists, deleting existing file {destinationFilePath}" );
                        // Delete the existing file
                        File.Delete( destinationFilePath );
                    }

                    // Move the file
                    Logger.Log(
                        $"Move '{Path.GetFileName( sourcePath )}' to '{destinationFilePath}'" );

                    File.Move( sourcePath, destinationFilePath );
                }

                return true;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        public async Task<bool> ExecuteTSLPatcherAsync(
            Utility.Utility.IConfirmationDialogCallback confirmDialog
        )
        {
            try
            {
                foreach ( string t in sourcePaths )
                {
                    string tslPatcherPath = t;
                    DirectoryInfo tslPatcherDirectory;

                    if ( Path.HasExtension( tslPatcherPath ) )
                    {
                        // It's a file, get the parent folder.
                        tslPatcherDirectory = new FileInfo( tslPatcherPath ).Directory;

                        if ( tslPatcherDirectory == null || !tslPatcherDirectory.Exists )
                        {
                            throw new DirectoryNotFoundException(
                                $"The parent directory of the file {tslPatcherPath} could not be located on the disk." );
                        }
                    }
                    else
                    {
                        // It's a folder, create a DirectoryInfo instance
                        tslPatcherDirectory = new DirectoryInfo( tslPatcherPath );

                        if ( !tslPatcherDirectory.Exists )
                        {
                            throw new DirectoryNotFoundException(
                                $"The directory {tslPatcherPath} could not be located on the disk." );
                        }
                    }

                    Logger.Log( "Installing TSLPatcher LookUpGameFolder hook..." );
                    FileHelper.ReplaceLookupGameFolder( tslPatcherDirectory );

                    string args = $@"""{MainConfig.DestinationPath}""" // arg1 = swkotor directory
                                  + $@" ""{MainConfig.SourcePath}"""   // arg2 = mod directory (where tslpatcherdata folder is)
                                  + ( string.IsNullOrEmpty( this.Arguments )
                                      ? "" : $" {this.Arguments}" );   // arg3 = (optional) install option integer index from namespaces.ini

                    var tslPatcherCliPath = new FileInfo(
                        Path.Combine(
                            FileHelper.ResourcesDirectory,
                            "TSLPatcherCLI.exe"
                        )
                    );

                    Logger.Log( "Run TSLPatcher..." );
                    (int exitCode, string output, string error)
                        = await PlatformAgnosticMethods.ExecuteProcessAsync( tslPatcherCliPath, args );
                    Logger.LogVerbose( $"{tslPatcherCliPath.Name} exited with exit code {exitCode}" );

                    Logger.Log( !string.IsNullOrEmpty( output ) ? output : null );
                    Logger.Log( !string.IsNullOrEmpty( error ) ? error : null );

                    return exitCode == 0;
                }

                return false;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                throw;
            }
        }

        public async Task<bool> ExecuteProgramAsync(
            Utility.Utility.IConfirmationDialogCallback confirmDialog
        )
        {
            try
            {
                bool isSuccess = true; // Track the success status
                foreach ( string sourcePath in sourcePaths )
                {
                    try
                    {
                        if ( this.Action == "TSLPatcher" )
                        {
                            FileHelper.ReplaceLookupGameFolder(
                                new DirectoryInfo( Path.GetDirectoryName( sourcePath ) ?? string.Empty )
                            );
                        }

                        var thisProgram = new FileInfo( sourcePath );
                        if ( !thisProgram.Exists )
                        {
                            throw new FileNotFoundException(
                                $"The file {sourcePath} could not be located on the disk"
                            );
                        }

                        (int exitCode, string output, string error) = await PlatformAgnosticMethods.ExecuteProcessAsync( thisProgram );
                        if ( exitCode == 0 )
                            continue;

                        isSuccess = false;
                        break;
                    }
                    catch ( Exception ex )
                    {
                        Logger.LogException( ex );
                        return false;
                    }
                }

                return isSuccess;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        // parse TSLPatcher's installlog.rtf for errors, since there's no CLI.
        public List<string> VerifyInstall()
        {
            foreach ( string sourcePath in sourcePaths )
            {
                if ( sourcePath == null )
                    continue;

                string tslPatcherDirPath
                    = Path.GetDirectoryName( sourcePath )
                      ?? throw new DirectoryNotFoundException( $"Could not retrieve parent directory of {sourcePath}." );

                string fullInstallLogFile = Path.Combine(
                    tslPatcherDirPath,
                    "installlog.rtf"
                );

                if ( !File.Exists( fullInstallLogFile ) )
                    throw new FileNotFoundException( "Install log file not found.", fullInstallLogFile );

                string installLogContent = File.ReadAllText( fullInstallLogFile );
                foreach ( string thisLine in installLogContent.Split( '\n' ) )
                {
                    if ( !thisLine.Contains( "Error: " ) )
                        continue;

                    new List<string>().Add( thisLine );
                }
            }

            return new List<string>();
        }
    }
}
