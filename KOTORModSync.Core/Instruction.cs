// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

namespace KOTORModSync.Core
{
    public class Instruction : INotifyPropertyChanged
    {
        public static List<string> AvailableActions = new List<string>()
        {
            "execute",
            "tslpatcher",
            "move",
            "rename",
            "delete",
            "delduplicate",
        };

        public string Action { get; set; }
        [NotNull][ItemNotNull] public List<string> Source { get => _source; set { _source = value; OnPropertyChanged(); } }
        [NotNull] public string Destination { get => _destination; set { _destination = value; OnPropertyChanged(); } }
        [NotNull] public List<Guid> Dependencies { get => _dependencies; set { _dependencies = value; OnPropertyChanged(); } }
        [NotNull] public List<Guid> Restrictions { get => _restrictions; set { _restrictions = value; OnPropertyChanged(); } }
        public bool Overwrite { get; set; }
        public string Arguments { get; set; }
        private Component ParentComponent { get; set; }
        public Dictionary<FileInfo, SHA1> ExpectedChecksums { get; set; }
        public Dictionary<FileInfo, SHA1> OriginalChecksums { get; internal set; }

        public enum ActionExitCode
        {
            UnauthorizedAccessException = -1,
            Success,
            InvalidSelfExtractingExecutable,
            InvalidArchive,
            ArchiveParseError,
            FileNotFoundPost,
            IOException,
            RenameTargetAlreadyExists,
            TSLPatcherCLIError,
            ChildProcessError,
            UnknownError,
            UnknownInnerError,
            TSLPatcherError,
            UnknownInstruction,
            TSLPatcherLogNotFound
        }

        public static readonly string DefaultInstructions = @"
[[thisMod.instructions]]
action = ""extract""
source = [""<<modDirectory>>\\path\\to\\mod\\mod.rar""]
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
source = [""<<modDirectory>>\\path\\to\\mod\\TSLPatcher directory""]
arguments = ""any command line arguments to pass (in TSLPatcher, this is the index of the desired option in namespaces.ini))""
";

        [NotNull][ItemNotNull] private List<string> _source = new List<string>();
        [NotNull] private string _destination = string.Empty;
        [NotNull] private List<Guid> _dependencies = new List<Guid>();
        [NotNull] private List<Guid> _restrictions = new List<Guid>();
        [NotNull][ItemNotNull] private List<string> RealSourcePaths { get; set; } = new List<string>();
        [CanBeNull] private DirectoryInfo RealDestinationPath { get; set; }

        public void SetParentComponent( [CanBeNull] Component parentComponent ) => ParentComponent = parentComponent;
        public static async Task<bool> ExecuteInstructionAsync( [NotNull] Func<Task<bool>> instructionMethod ) =>
            await (instructionMethod() ?? throw new ArgumentNullException( nameof(instructionMethod) )).ConfigureAwait( false );

        // This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
        // This method should not be ran before an instruction is executed.
        // Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
        // ^ perhaps the above is user error though? User should check what they are running in advance perhaps? Either way, we attempt to baby them here.
        public void SetRealPaths( bool noValidate = false )
        {
            // Get real path then enumerate the files/folders with wildcards and add them to the list
            if ( Source is null ) throw new NullReferenceException( nameof( Source ) );

            RealSourcePaths = Source.ConvertAll( Utility.Utility.ReplaceCustomVariables );
            RealSourcePaths = FileHelper.EnumerateFilesWithWildcards( RealSourcePaths );

            if ( RealSourcePaths is null || ( !noValidate && RealSourcePaths.Count == 0 ) )
            {
                throw new FileNotFoundException(
                    $"Could not find any files in the 'Source' path! Got [{string.Join( ", ", Source )}]"
                );
            }

            string destinationPath = Utility.Utility.ReplaceCustomVariables( Destination );
            if ( string.IsNullOrWhiteSpace( destinationPath ) )
                return;

            var thisDestination = new DirectoryInfo( destinationPath );
            if ( !thisDestination.Exists )
            {
                (FileSystemInfo caseSensitiveDestination, List<string> isMultiple)
                    = PlatformAgnosticMethods.GetClosestMatchingEntry( thisDestination.FullName );

                thisDestination = (DirectoryInfo)caseSensitiveDestination
                    ?? throw new DirectoryNotFoundException( "Could not find the 'Destination' path!" );
            }

            RealDestinationPath = thisDestination;
        }

        public async Task<ActionExitCode> ExtractFileAsync(
            DirectoryInfo argDestinationPath = null,
            List<string> argSourcePaths = null
        )
        {
            try
            {
                if ( argSourcePaths is null )
                {
                    argSourcePaths = RealSourcePaths
                        ?? throw new NullReferenceException( nameof( RealSourcePaths ) );
                    //argDestinationPath = RealDestinationPath; // not used in this action.
                }

                var extractionTasks = new List<Task>( 25 );
                var semaphore = new SemaphoreSlim( 5 ); // Limiting to 5 concurrent extractions
                ActionExitCode exitCode = ActionExitCode.Success;

                foreach ( string sourcePath in argSourcePaths )
                {
                    await semaphore.WaitAsync(); // Acquire a semaphore slot

                    extractionTasks.Add( Task.Run( InnerExtractFileAsync ) );

                    async Task InnerExtractFileAsync()
                    {
                        try
                        {
                            var thisFile = new FileInfo( sourcePath );
                            if ( argDestinationPath is null )
                            {
                                argDestinationPath = thisFile.Directory;
                            }

                            if ( argDestinationPath is null )
                            {
                                throw new ArgumentNullException( nameof( argDestinationPath ) );
                            }

                            _ = Logger.LogAsync( $"File path: {thisFile.FullName}" );

                            if ( !ArchiveHelper.IsArchive( thisFile ) )
                            {
                                if ( !( ParentComponent is null ) )
                                {
                                    _ = Logger.LogAsync(
                                        $"[Error] '{ParentComponent.Name}' failed to extract file '{thisFile.Name}'. Invalid archive?"
                                    );
                                }

                                exitCode = ActionExitCode.InvalidArchive;
                                return;
                            }

                            if ( thisFile.Extension.Equals( ".exe", StringComparison.OrdinalIgnoreCase ) )
                            {
                                (int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                    thisFile,
                                    $" -o\"{thisFile.DirectoryName}\" -y",
                                    noAdmin: MainConfig.NoAdmin
                                );

                                if ( result.Item1 == 0 )
                                {
                                    return;
                                }

                                exitCode = ActionExitCode.InvalidSelfExtractingExecutable;
                                throw new InvalidOperationException(
                                    $"'{thisFile.Name}' is not a self-extracting executable as previously assumed. Cannot extract."
                                );
                            }

                            using ( FileStream stream = File.OpenRead( thisFile.FullName ) )
                            {
                                IArchive archive = null;

                                switch ( thisFile.Extension )
                                {
                                    case ".zip":
                                        archive = SharpCompress.Archives.Zip.ZipArchive.Open( stream );
                                        break;
                                    case ".rar":
                                        archive = RarArchive.Open( stream );
                                        break;
                                    case ".7z":
                                        archive = SevenZipArchive.Open( stream );
                                        break;
                                }

                                if ( archive is null )
                                {
                                    exitCode = ActionExitCode.ArchiveParseError;
                                    throw new InvalidOperationException( $"Unable to parse archive '{sourcePath}'" );
                                }

                                IReader reader = archive.ExtractAllEntries();
                                while ( reader.MoveToNextEntry() )
                                {
                                    if ( reader.Entry.IsDirectory )
                                    {
                                        continue;
                                    }

                                    if ( argDestinationPath?.FullName is null )
                                    {
                                        continue;
                                    }

                                    string extractFolderName = Path.GetFileNameWithoutExtension( thisFile.Name );
                                    string destinationItemPath = Path.Combine(
                                        argDestinationPath.FullName,
                                        extractFolderName,
                                        reader.Entry.Key
                                    );
                                    string destinationDirectory = Path.GetDirectoryName( destinationItemPath );

                                    if ( destinationDirectory != null && !Directory.Exists( destinationDirectory ) )
                                    {
                                        _ = Logger.LogAsync( $"Create directory '{destinationDirectory}'" );
                                        _ = Directory.CreateDirectory( destinationDirectory );
                                    }

                                    _ = Logger.LogAsync(
                                        $"Extract '{reader.Entry.Key}' to '{argDestinationPath.FullName}'"
                                    );

                                    try
                                    {
                                        await Task.Run(
                                            () => reader.WriteEntryToDirectory(
                                                destinationDirectory ?? throw new InvalidOperationException(),
                                                ArchiveHelper.DefaultExtractionOptions
                                            )
                                        );
                                    }
                                    catch ( UnauthorizedAccessException )
                                    {
                                        _ = Logger.LogWarningAsync(
                                            $"Skipping file '{reader.Entry.Key}' due to lack of permissions."
                                        );
                                        //exitCode = ActionExitCode.UnauthorizedAccessException;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            _ = semaphore.Release(); // Release the semaphore slot
                        }
                    }
                }

                await Task.WhenAll( extractionTasks ); // Wait for all extraction tasks to complete

                return exitCode; // Extraction succeeded
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                return ActionExitCode.UnknownError; // Extraction failed
            }
        }


        public void DeleteDuplicateFile( DirectoryInfo directoryPath = null, string fileExtension = "" )
        {
            if ( directoryPath is null )
            {
                directoryPath = RealDestinationPath;
            }
            else if ( !directoryPath.Exists )
            {
                throw new ArgumentException( "Invalid directory path.", nameof( directoryPath ) );
            }

            if ( string.IsNullOrEmpty( fileExtension ) )
            {
                fileExtension = Arguments;
            }

            string[] files = Directory.GetFiles( directoryPath.FullName );
            var fileNameCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
            ;

            foreach ( string filePath in files )
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( filePath );
                if ( string.IsNullOrEmpty( fileNameWithoutExtension ) ) continue;

                string extension = Path.GetExtension( filePath );
                if ( !extension.Equals( ".tga", StringComparison.OrdinalIgnoreCase )
                    && !extension.Equals( ".tpc", StringComparison.OrdinalIgnoreCase ) )
                {
                    continue;
                }

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
                    || fileNameCounts[fileNameWithoutExtension] <= 1
                    || !string.Equals( fileExtensionFromFile, fileExtension, StringComparison.OrdinalIgnoreCase ) )
                {
                    continue;
                }

                try
                {
                    File.Delete( filePath );
                    Logger.Log( $"Deleted file: '{fileName}'" );
                }
                catch ( Exception ex )
                {
                    Logger.LogException( ex );
                }
            }
        }

        public ActionExitCode DeleteFile()
        {
            try
            {
                foreach ( string thisFilePath in RealSourcePaths )
                {
                    var thisFile = new FileInfo( thisFilePath );

                    if ( !Path.IsPathRooted( thisFilePath ) || !thisFile.Exists )
                    {
                        var ex = new ArgumentNullException(
                            $"Invalid wildcards or file does not exist: '{thisFilePath}'"
                        );
                        Logger.LogException( ex );
                        return ActionExitCode.FileNotFoundPost;
                    }

                    // Delete the file synchronously
                    try
                    {
                        File.Delete( thisFilePath );
                        Logger.Log( $"Deleting '{thisFilePath}'..." );
                    }
                    catch ( Exception ex )
                    {
                        Logger.LogException( ex );
                        return ActionExitCode.UnknownInnerError;
                    }
                }

                return ActionExitCode.Success;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return ActionExitCode.UnknownError;
            }
        }

        public ActionExitCode RenameFile()
        {
            try
            {
                ActionExitCode exitCode = ActionExitCode.Success;
                foreach ( string sourcePath in Source.ConvertAll( Utility.Utility.ReplaceCustomVariables ) )
                {
                    // Check if the source file already exists
                    string fileName = Path.GetFileName( sourcePath );
                    if ( !File.Exists( sourcePath ) )
                    {
                        Logger.Log( $"'{fileName}' does not exist!" );
                        exitCode = ActionExitCode.FileNotFoundPost;
                        continue;
                    }

                    // Check if the destination file already exists
                    string destinationFilePath = Path.Combine(
                        Path.GetDirectoryName( sourcePath ) ?? string.Empty,
                        Destination
                    );
                    if ( File.Exists( destinationFilePath ) )
                    {
                        if ( Overwrite )
                        {
                            Logger.Log( $"Replacing pre-existing '{destinationFilePath}'" );
                            File.Delete( destinationFilePath );
                        }
                        else
                        {
                            exitCode = ActionExitCode.RenameTargetAlreadyExists;
                            Logger.LogException(
                                new InvalidOperationException(
                                    $"Skipping file '{sourcePath}'"
                                    + $" ( A file with the name '{Path.GetFileName( destinationFilePath )}'"
                                    + " already exists )"
                                )
                            );
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
                        exitCode = ActionExitCode.IOException;
                        Logger.LogException( ex );
                    }
                }

                return exitCode;
            }
            catch ( Exception ex )
            {
                // Handle any unexpected exceptions
                Logger.LogException( ex );
                return ActionExitCode.UnknownError;
            }
        }

        public ActionExitCode CopyFile()
        {
            try
            {
                foreach ( string sourcePath in RealSourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string destinationFilePath = Path.Combine( RealDestinationPath.FullName, fileName );

                    // Check if the destination file already exists
                    if ( !Overwrite && File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"Skipping file '{Path.GetFileName( destinationFilePath )}' ( Overwrite set to False )"
                        );
                        continue;
                    }

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        Logger.Log( $"File already exists, deleting existing file '{destinationFilePath}'" );
                        // Delete the existing file
                        File.Delete( destinationFilePath );
                    }

                    // Copy the file
                    Logger.Log( $"Copy '{Path.GetFileName( sourcePath )}' to '{destinationFilePath}'" );

                    File.Copy( sourcePath, destinationFilePath );
                }

                return ActionExitCode.Success;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return ActionExitCode.UnknownError;
            }
        }

        public ActionExitCode MoveFile()
        {
            try
            {
                foreach ( string sourcePath in RealSourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string destinationFilePath = Path.Combine( RealDestinationPath.FullName, fileName );

                    // Check if the destination file already exists
                    if ( !Overwrite && File.Exists( destinationFilePath ) )
                    {
                        Logger.Log(
                            $"Skipping file '{Path.GetFileName( destinationFilePath )}' ( Overwrite set to False )"
                        );

                        continue;
                    }

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        Logger.Log( $"File already exists, deleting pre-existing file '{destinationFilePath}'" );
                        // Delete the existing file
                        File.Delete( destinationFilePath );
                    }

                    // Move the file
                    Logger.Log( $"Move '{Path.GetFileName( sourcePath )}' to '{destinationFilePath}'" );

                    File.Move( sourcePath, destinationFilePath );
                }

                return ActionExitCode.Success;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return ActionExitCode.UnknownError;
            }
        }

        // todo: define exit codes here.
        public async Task<ActionExitCode> ExecuteTSLPatcherAsync()
        {
            try
            {
                foreach ( string t in RealSourcePaths )
                {
                    DirectoryInfo tslPatcherDirectory = Path.HasExtension( t )
                        ? new FileInfo( t ).Directory // It's a file, get the parent folder.
                        : new DirectoryInfo( t ); // It's a folder, create a DirectoryInfo instance

                    if ( tslPatcherDirectory is null || !tslPatcherDirectory.Exists )
                    {
                        throw new DirectoryNotFoundException(
                            $"The directory '{t}' could not be located on the disk."
                        );
                    }

                    //PlaintextLog=0
                    string fullInstallLogFile = Path.Combine( tslPatcherDirectory.FullName, "installlog.rtf" );
                    if ( File.Exists( fullInstallLogFile ) )
                    {
                        if ( File.Exists( fullInstallLogFile ) )
                        {
                            File.Delete( fullInstallLogFile );
                        }
                    }

                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirectory.FullName, "installlog.txt" );
                    if ( File.Exists( fullInstallLogFile ) )
                    {
                        File.Delete( fullInstallLogFile );
                    }

                    await Logger.LogAsync( " - Pre-installing TSLPatcher LookUpGameFolder hooks..." );
                    IniHelper.ReplacePlaintextLog( tslPatcherDirectory );
                    IniHelper.ReplaceLookupGameFolder( tslPatcherDirectory );

                    string args = $@"""{MainConfig.DestinationPath}""" // arg1 = swkotor directory
                        + $@" ""{tslPatcherDirectory}""" // arg2 = mod directory (where tslpatchdata folder is)
                        + ( string.IsNullOrEmpty( Arguments )
                            ? ""
                            : $" {Arguments}" ); // arg3 = (optional) install option integer index from namespaces.ini

                    string thisExe = null;
                    FileInfo tslPatcherCliPath = null;
                    switch ( MainConfig.PatcherOption )
                    {
                        case MainConfig.AvailablePatchers.PyKotorCLI:
                            thisExe = Path.Combine(
                                "Resources",
                                RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
                                    ? "pykotorcli.exe" // windows
                                    : "pykotorcli" // linux/mac
                            );
                            break;
                        case MainConfig.AvailablePatchers.TSLPatcher:
                        default:
                            tslPatcherCliPath = new FileInfo( t );
                            break;
                    }

                    if ( tslPatcherCliPath is null )
                    {
                        string executingAssemblyLocation = Utility.Utility.GetExecutingAssemblyDirectory();

                        tslPatcherCliPath = new FileInfo(
                            Path.Combine(
                                executingAssemblyLocation,
                                thisExe
                            )
                        );
                    }


                    await Logger.LogAsync( "Starting TSLPatcher instructions..." );
                    if ( MainConfig.PatcherOption != MainConfig.AvailablePatchers.TSLPatcher )
                    {
                        await Logger.LogVerboseAsync( $"Using CLI to run command: '{tslPatcherCliPath} {args}'" );
                    }

                    (int exitCode, string output, string error) = await PlatformAgnosticMethods.ExecuteProcessAsync(
                        tslPatcherCliPath,
                        args,
                        noAdmin: MainConfig.NoAdmin
                    );
                    await Logger.LogVerboseAsync( $"'{tslPatcherCliPath.Name}' exited with exit code {exitCode}" );

                    await Logger.LogAsync( output );
                    await Logger.LogAsync( error );

                    try
                    {
                        List<string> installErrors = VerifyInstall();
                        if ( installErrors.Count > 0 )
                        {
                            await Logger.LogAsync( string.Join( Environment.NewLine, installErrors ) );
                            return ActionExitCode.TSLPatcherError;
                        }
                    }
                    catch ( FileNotFoundException )
                    {
                        await Logger.LogAsync( "No TSLPatcher log file found!" );
                        return ActionExitCode.TSLPatcherLogNotFound;
                    }


                    return exitCode == 0
                        ? ActionExitCode.Success
                        : ActionExitCode.TSLPatcherCLIError;
                }

                return ActionExitCode.Success;
            }
            catch ( DirectoryNotFoundException ex )
            {
                await Logger.LogExceptionAsync( ex );
                throw;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                throw;
            }
        }

        public async Task<ActionExitCode> ExecuteProgramAsync()
        {
            try
            {
                if ( RealSourcePaths is null ) throw new NullReferenceException( nameof( RealSourcePaths ) );

                ActionExitCode exitCode = ActionExitCode.Success; // Track the success status
                foreach ( string sourcePath in RealSourcePaths )
                {
                    try
                    {
                        var thisProgram = new FileInfo( sourcePath );
                        if ( !thisProgram.Exists )
                        {
                            throw new FileNotFoundException(
                                $"The file '{sourcePath}' could not be located on the disk"
                            );
                        }

                        (int childExitCode, string output, string error)
                            = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                thisProgram,
                                noAdmin: MainConfig.NoAdmin
                            );

                        _ = Logger.LogVerboseAsync( output + Environment.NewLine + error );
                        if ( childExitCode == 0 )
                        {
                            continue;
                        }

                        exitCode = ActionExitCode.ChildProcessError;
                        break;
                    }
                    catch ( FileNotFoundException ex )
                    {
                        await Logger.LogExceptionAsync( ex );
                        return ActionExitCode.FileNotFoundPost;
                    }
                    catch ( Exception ex )
                    {
                        await Logger.LogExceptionAsync( ex );
                        return ActionExitCode.UnknownInnerError;
                    }
                }

                return exitCode;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                return ActionExitCode.UnknownError;
            }
        }

        // parse TSLPatcher's installlog.rtf (or installlog.txt) for errors when not using the CLI.
        public List<string> VerifyInstall()
        {
            foreach ( string sourcePath in RealSourcePaths )
            {
                if ( sourcePath is null )
                {
                    continue;
                }

                string tslPatcherDirPath = Path.GetDirectoryName( sourcePath )
                    ?? throw new DirectoryNotFoundException(
                        $"Could not retrieve parent directory of '{sourcePath}'."
                    );

                //PlaintextLog=0
                string fullInstallLogFile = Path.Combine( tslPatcherDirPath, "installlog.rtf" );

                if ( !File.Exists( fullInstallLogFile ) )
                {
                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirPath, "installlog.txt" );
                    if ( !File.Exists( fullInstallLogFile ) )
                    {
                        throw new FileNotFoundException( "Install log file not found.", fullInstallLogFile );
                    }
                }

                string installLogContent = File.ReadAllText( fullInstallLogFile );
                var installErrors = new List<string>();
                foreach ( string thisLine in installLogContent.Split(
                             Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar
                         ) )
                {
                    if ( !thisLine.Contains( "Error: " ) && !thisLine.Contains( "[Error]" ) )
                    {
                        continue;
                    }

                    installErrors.Add( thisLine );
                }

                return installErrors;
            }

            Logger.LogVerbose( "No errors found in TSLPatcher installlog file" );
            return new List<string>();
        }

        // this method removes the tslpatcher log file.
        // should be called BEFORE any tslpatcher install takes place from KOTORModSync, never post-install.
        public void CleanupTSLPatcherInstall()
        {
            Logger.LogVerbose( "Preparing TSLPatcher install..." );
            foreach ( string sourcePath in RealSourcePaths )
            {
                string tslPatcherDirPath = Path.GetDirectoryName( sourcePath )
                    ?? throw new DirectoryNotFoundException(
                        $"Could not retrieve parent directory of '{sourcePath}'."
                    );

                //PlaintextLog=0
                string fullInstallLogFile = Path.Combine( tslPatcherDirPath, "installlog.rtf" );

                if ( !File.Exists( fullInstallLogFile ) )
                {
                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirPath, "installlog.txt" );
                    if ( !File.Exists( fullInstallLogFile ) )
                    {
                        Logger.LogVerbose( $"No prior install found for {sourcePath}" );
                        return;
                    }
                }

                Logger.LogVerbose( $"Delete {fullInstallLogFile}" );
                File.Delete( fullInstallLogFile );
            }

            Logger.LogVerbose( "Finished cleaning tslpatcher install" );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );

        protected bool SetField<T>(
            [CanBeNull] ref T field,
            [CanBeNull] T value,
            [CallerMemberName][CanBeNull] string propertyName = null
        )
        {
            if ( EqualityComparer<T>.Default.Equals( field, value ) )
            {
                return false;
            }

            field = value;
            OnPropertyChanged( propertyName );
            return true;
        }

        [NotNull]
        public Option GetChosenOption()
        {
            if ( ParentComponent is null )
                throw new NullReferenceException( "ParentComponent not found for this instruction!" );

            foreach ( KeyValuePair<Guid, Option> kvp in ParentComponent.ChosenOptions )
            {
                Guid optionGuid = kvp.Key;
                Option thisOption = kvp.Value;

                Option thisInstructionOption = this.Options.FirstOrDefault( o => o.Guid == optionGuid );
                if ( thisInstructionOption is null )
                    continue;

                if ( thisOption != thisInstructionOption )
                    throw new DuplicateNameException( "This guid already corresponds to another option." );

                return thisInstructionOption;
            }

            throw new KeyNotFoundException("Could not find chosen option for this instruction");
        }


        [NotNull][ItemNotNull] public List<Option> Options = new List<Option>();
    }
}
