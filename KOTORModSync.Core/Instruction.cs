// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Data;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using static KOTORModSync.Core.Instruction;

namespace KOTORModSync.Core
{
    public sealed class Instruction : INotifyPropertyChanged
    {
        public enum ActionType
        {
            Extract,
            Execute,
            TSLPatcher,
            Move,
            Copy,
            Rename,
            Delete,
            DelDuplicate,
            Choose,
            HoloPatcher,
            Run,
            Unset,
        }

        public static IEnumerable<string> ActionTypes => Enum.GetValues(typeof(ActionType))
            .Cast<ActionType>()
            .Select(actionType => actionType.ToString());


        private ActionType _action;
        [JsonIgnore]
        public ActionType Action
        {
            get => _action;
            set
            {
                _action = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("Action")]
        public string ActionString
        {
            get => Action.ToString();
            set => Action = (ActionType)Enum.Parse( typeof( ActionType ), value );
        }
        
        [NotNull][ItemNotNull] private List<string> _source = new List<string>();
        [NotNull][ItemNotNull] public List<string> Source
        {
            get => _source;
            set
            {
                _source = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _destination = string.Empty;
        [NotNull] public string Destination
        {
            get => _destination;
            set
            {
                _destination = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private List<Guid> _dependencies = new List<Guid>();
        [NotNull] public List<Guid> Dependencies
        {
            get => _dependencies;
            set
            {
                _dependencies = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private List<Guid> _restrictions = new List<Guid>();
        [NotNull] public List<Guid> Restrictions
        {
            get => _restrictions;
            set
            {
                _restrictions = value;
                OnPropertyChanged();
            }
        }
        
        private bool _overwrite;
        public bool Overwrite
        {
            get => _overwrite;
            set
            {
                _overwrite = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _arguments = string.Empty;
        [NotNull] public string Arguments
        {
            get => _arguments;
            set
            {
                _arguments = value;
                OnPropertyChanged();
            }
        }

        [NotNull][ItemNotNull] private List<string> RealSourcePaths { get; set; } = new List<string>();
        [CanBeNull] private DirectoryInfo RealDestinationPath { get; set; }

        private Component _parentComponent { get; set; }
        
        public Component GetParentComponent() => _parentComponent;
        public void SetParentComponent(Component thisComponent) => _parentComponent = thisComponent;

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
            TSLPatcherLogNotFound,
        }

        public static async Task<bool> ExecuteInstructionAsync( [NotNull] Func<Task<bool>> instructionMethod ) =>
            await ( instructionMethod()
                ?? throw new ArgumentNullException( nameof( instructionMethod ) ) ).ConfigureAwait( false );

        // This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
        // This method should not be ran before an instruction is executed.
        // Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
        // ^ perhaps the above is user error though? User should check what they are running in advance perhaps? Either way, we attempt to baby them here.
        internal void SetRealPaths( bool noValidate = false )
        {
            // Get real path then enumerate the files/folders with wildcards and add them to the list
            if ( Source is null )
                throw new NullReferenceException( nameof( Source ) );

            List<string> newSourcePaths = Source.ConvertAll( Utility.Utility.ReplaceCustomVariables );
            newSourcePaths = PathHelper.EnumerateFilesWithWildcards( newSourcePaths );

            if ( newSourcePaths.IsNullOrEmptyOrAllNull() && !noValidate )
            {
                throw new FileNotFoundException(
                    $"Could not find any files in the 'Source' path! Got [{string.Join( separator: ", ", Source )}]"
                );
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            RealSourcePaths = newSourcePaths;

            string destinationPath = Utility.Utility.ReplaceCustomVariables( Destination );
            if ( string.IsNullOrWhiteSpace( destinationPath ) )
                return;

            DirectoryInfo thisDestination = PathValidator.IsValidPath( destinationPath )
                ? new DirectoryInfo( destinationPath )
                : throw new InvalidDataException( destinationPath + " is not a valid path!" );

            if ( !thisDestination.Exists )
            {
                string caseSensitiveDestination = PathHelper.GetCaseSensitivePath( thisDestination.FullName );
                if ( !noValidate && caseSensitiveDestination is null )
                    throw new DirectoryNotFoundException( "Could not find the 'Destination' path!" );

                if ( caseSensitiveDestination is null )
                    return;

                thisDestination = new DirectoryInfo( caseSensitiveDestination );
            }

            RealDestinationPath = thisDestination;
        }

        public async Task<ActionExitCode> ExtractFileAsync(DirectoryInfo argDestinationPath = null, [NotNull][ItemNotNull] List<string> argSourcePaths = null)
        {
            try
            {
                RealSourcePaths = argSourcePaths ?? RealSourcePaths ?? throw new ArgumentNullException(nameof(argSourcePaths));
                
                var semaphore = new SemaphoreSlim(initialCount: 5); // Limiting to 5 concurrent extractions
                List<Task> extractionTasks = RealSourcePaths.Select( InnerExtractFileAsync ).ToList();
                ActionExitCode exitCode = ActionExitCode.Success;

                async Task InnerExtractFileAsync(string sourcePath)
                {
                    try
                    {
                        var thisFile = new FileInfo(sourcePath);
                        argDestinationPath = argDestinationPath
                            ?? thisFile.Directory
                            ?? throw new ArgumentNullException(nameof(argDestinationPath));

                        _ = Logger.LogAsync($"File path: '{thisFile.FullName}'");

                        if (!ArchiveHelper.IsArchive(thisFile))
                        {
                            if (!(_parentComponent is null))
                                _ = Logger.LogAsync( $"[Error] '{_parentComponent.Name}' failed to extract file '{thisFile.Name}'. Invalid archive?" );

                            exitCode = ActionExitCode.InvalidArchive;
                            return;
                        }

                        if (thisFile.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            (int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                thisFile.FullName,
                                $" -o\"{thisFile.DirectoryName}\" -y",
                                noAdmin: MainConfig.NoAdmin
                            );

                            if (result.Item1 == 0)
                                return;

                            exitCode = ActionExitCode.InvalidSelfExtractingExecutable;
                            throw new InvalidOperationException($"'{thisFile.Name}' is not a self-extracting executable as previously assumed. Cannot extract.");
                        }

                        using (FileStream stream = File.OpenRead(thisFile.FullName))
                        {
                            IArchive archive = null;

                            switch ( thisFile.Extension.ToLowerInvariant() )
                            {
                                case ".zip":
                                    archive = SharpCompress.Archives.Zip.ZipArchive.Open(stream);
                                    break;
                                case ".rar":
                                    archive = RarArchive.Open(stream);
                                    break;
                                case ".7z":
                                    archive = SevenZipArchive.Open(stream);
                                    break;
                            }

                            if (archive is null)
                            {
                                exitCode = ActionExitCode.ArchiveParseError;
                                throw new InvalidOperationException($"Unable to parse archive '{sourcePath}'");
                            }

                            IReader reader = archive.ExtractAllEntries();
                            while (reader.MoveToNextEntry())
                            {
                                if (reader.Entry.IsDirectory)
                                    continue;

                                if (argDestinationPath?.FullName is null)
                                    continue;

                                string extractFolderName = Path.GetFileNameWithoutExtension(thisFile.Name);
                                string destinationItemPath = Path.Combine(
                                    argDestinationPath.FullName,
                                    extractFolderName,
                                    reader.Entry.Key
                                );
                                string destinationDirectory = Path.GetDirectoryName(destinationItemPath);

                                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                                {
                                    _ = Logger.LogAsync($"Create directory '{destinationDirectory}'");
                                    _ = Directory.CreateDirectory(destinationDirectory);
                                }

                                _ = Logger.LogAsync($"Extract '{reader.Entry.Key}' to '{argDestinationPath.FullName}'");

                                try
                                {
                                    await Task.Run(
                                        () => reader.WriteEntryToDirectory(
                                            destinationDirectory ?? throw new InvalidOperationException(),
                                            ArchiveHelper.DefaultExtractionOptions
                                        )
                                    );
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    _ = Logger.LogWarningAsync($"Skipping file '{reader.Entry.Key}' due to lack of permissions.");
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

                await Task.WhenAll(extractionTasks); // Wait for all extraction tasks to complete

                return exitCode; // Extraction succeeded
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex);
                return ActionExitCode.UnknownError; // Extraction failed
            }
        }


        public void DeleteDuplicateFile(
            DirectoryInfo directoryPath = null,
            string fileExtension = null,
            bool caseInsensitive = false,
            List<string> compatibleExtensions = null
        )
        {
            // internal args
            if ( directoryPath is null )
                directoryPath = RealDestinationPath;
            if ( directoryPath?.Exists != true )
                throw new ArgumentException( message: "Invalid directory path.", nameof( directoryPath ) );

            if ( string.IsNullOrEmpty( fileExtension ) )
                fileExtension = Arguments;

            if ( compatibleExtensions.IsNullOrEmptyCollection() )
                compatibleExtensions = Game.TextureOverridePriorityList;

            string[] files = Directory.GetFiles( directoryPath.FullName );
            Dictionary<string, int> fileNameCounts = caseInsensitive
                ? new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase )
                : new Dictionary<string, int>();

            foreach ( string filePath in files )
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( filePath );
                string thisExtension = Path.GetExtension( filePath );

                bool compatibleExtensionFound = caseInsensitive
                    // ReSharper disable once AssignNullToNotNullAttribute
                    // ReSharper disable once PossibleNullReferenceException
                    ? compatibleExtensions.Any( ext => ext.Equals( thisExtension, StringComparison.OrdinalIgnoreCase ) )
                    // ReSharper disable once PossibleNullReferenceException
                    : compatibleExtensions.Contains( thisExtension );

                if ( compatibleExtensionFound )
                {
                    if ( fileNameCounts.TryGetValue( fileNameWithoutExtension, out int _ ) )
                        fileNameCounts[fileNameWithoutExtension]++;
                    else
                        fileNameCounts[fileNameWithoutExtension] = 1;
                }
            }

            foreach ( string filePath in files )
            {
                if ( !ShouldDeleteFile( filePath ) )
                    continue;

                try
                {
                    File.Delete( filePath );
                    Logger.Log( $"Deleted file: '{Path.GetFileNameWithoutExtension( filePath )}'" );
                }
                catch ( Exception ex )
                {
                    Logger.LogException( ex );
                }
            }

            bool ShouldDeleteFile( string filePath )
            {
                string fileName = Path.GetFileName( filePath );
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( filePath );
                string fileExtensionFromFile = Path.GetExtension( filePath );

                if ( string.IsNullOrEmpty( fileNameWithoutExtension ) )
                {
                    Logger.LogVerbose( $"Conditional 1: fileNameWithoutExtension from '{fileName}' is null or empty" );
                }
                else if ( !fileNameCounts.ContainsKey( fileNameWithoutExtension ) )
                {
                    Logger.LogVerbose(
                        $"Conditional 2: '{fileNameWithoutExtension}' is not present in '{fileNameCounts}' ergo not a duplicate"
                    );
                }
                else if ( fileNameCounts[fileNameWithoutExtension] <= 1 )
                {
                    Logger.LogVerbose(
                        $"Conditional 3: '{fileNameWithoutExtension}' is the only file with this name."
                    );
                }
                else if ( !string.Equals( fileExtensionFromFile, fileExtension, StringComparison.OrdinalIgnoreCase ) )
                {
                    string caseInsensitivity = caseInsensitive
                        ? " (case-insensitive)"
                        : string.Empty;
                    string message =
                        $"Conditional 4: '{fileExtensionFromFile}' is not equal to '{fileExtension}'{caseInsensitivity}";
                    Logger.LogVerbose( message );
                }
                else
                {
                    return true;
                }

                return false;
            }
        }

        // ReSharper disable once AssignNullToNotNullAttribute
        public ActionExitCode DeleteFile( 
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null
        )
        {
            if ( sourcePaths.IsNullOrEmptyCollection() )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths.IsNullOrEmptyCollection() )
                throw new ArgumentNullException( nameof( sourcePaths ) );

            try
            {
                foreach ( string thisFilePath in sourcePaths )
                {
                    var thisFile = new FileInfo( PathHelper.GetCaseSensitivePath( thisFilePath ) );

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

        public ActionExitCode RenameFile(
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null
        )
        {
            if ( sourcePaths.IsNullOrEmptyCollection() )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths.IsNullOrEmptyCollection() )
                throw new ArgumentNullException( nameof( sourcePaths ) );

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
                        if ( !Overwrite )
                        {
                            exitCode = ActionExitCode.RenameTargetAlreadyExists;
                            Logger.Log(
                                $"Skipping file '{sourcePath}'"
                                + $" ( A file with the name '{Path.GetFileName( destinationFilePath )}'"
                                + " already exists )"
                            );

                            continue;
                        }

                        Logger.Log( $"Replacing pre-existing '{destinationFilePath}'" );
                        File.Delete( destinationFilePath );
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

        public ActionExitCode CopyFile(
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null,
            DirectoryInfo destinationPath = null
        )
        {
            if ( sourcePaths.IsNullOrEmptyCollection() )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths.IsNullOrEmptyCollection() )
                throw new ArgumentNullException( nameof( sourcePaths ) );

            if ( destinationPath?.Exists != true )
                destinationPath = RealDestinationPath;
            if ( destinationPath?.Exists != true )
                throw new ArgumentNullException( nameof( destinationPath ) );

            try
            {
                foreach ( string sourcePath in sourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    // ReSharper disable once PossibleNullReferenceException
                    string destinationFilePath = Path.Combine( RealDestinationPath.FullName, fileName );

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        if ( !Overwrite )
                        {
                            Logger.Log(
                                $"File already exists, skipping file '{Path.GetFileName( destinationFilePath )}' ( Overwrite set to False )"
                            );

                            continue;
                        }

                        Logger.Log( $"File already exists, deleting pre-existing file '{destinationFilePath}'" );
                        File.Delete( destinationFilePath );
                    }

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

        public ActionExitCode MoveFile(
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null,
            DirectoryInfo destinationPath = null
        )
        {
            if ( sourcePaths.IsNullOrEmptyCollection() )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths.IsNullOrEmptyCollection() )
                throw new ArgumentNullException( nameof( sourcePaths ) );

            if ( destinationPath?.Exists != true )
                destinationPath = RealDestinationPath;
            if ( destinationPath?.Exists != true )
                throw new ArgumentNullException( nameof( destinationPath ) );

            try
            {
                foreach ( string sourcePath in sourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string destinationFilePath = Path.Combine( destinationPath.FullName, fileName );

                    // Check if the destination file already exists
                    if ( File.Exists( destinationFilePath ) )
                    {
                        if ( !Overwrite )
                        {
                            Logger.Log(
                                $"File already exists, skipping file '{Path.GetFileName( destinationFilePath )}' ( Overwrite set to False )"
                            );

                            continue;
                        }

                        Logger.Log( $"File already exists, deleting pre-existing file '{destinationFilePath}'" );
                        File.Delete( destinationFilePath );
                    }

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
        // ReSharper disable once InconsistentNaming
        public async Task<ActionExitCode> ExecuteTSLPatcherAsync()
        {
            try
            {
                foreach ( string t in RealSourcePaths )
                {
                    DirectoryInfo tslPatcherDirectory = Path.HasExtension( t )
                        ? new FileInfo( t ).Directory // It's a file, get the parent folder.
                        : new DirectoryInfo( t );     // It's a folder, create a DirectoryInfo instance

                    if ( tslPatcherDirectory?.Exists != true )
                    {
                        throw new DirectoryNotFoundException(
                            $"The directory '{t}' could not be located on the disk."
                        );
                    }

                    //PlaintextLog=0
                    string fullInstallLogFile = Path.Combine( tslPatcherDirectory.FullName, path2: "installlog.rtf" );
                    if ( File.Exists( fullInstallLogFile ) )
                    {
                        if ( File.Exists( fullInstallLogFile ) )
                            File.Delete( fullInstallLogFile );
                    }

                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirectory.FullName, path2: "installlog.txt" );
                    if ( File.Exists( fullInstallLogFile ) )
                        File.Delete( fullInstallLogFile );

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
                                path1: "Resources",
                                RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
                                    ? "pykotorcli.exe" // windows
                                    : "pykotorcli"     // linux/mac
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

                    ( int exitCode, string output, string error ) = await PlatformAgnosticMethods.ExecuteProcessAsync(
                        tslPatcherCliPath.FullName,
                        args,
                        noAdmin: MainConfig.NoAdmin
                    );
                    await Logger.LogVerboseAsync( $"'{tslPatcherCliPath.Name}' exited with exit code {exitCode}" );

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

        public async Task<ActionExitCode> ExecuteProgramAsync( [ItemNotNull] List<string> sourcePaths = null )
        {
            try
            {
                if ( sourcePaths == null )
                    sourcePaths = RealSourcePaths;
                if ( sourcePaths == null )
                    throw new ArgumentNullException( nameof( sourcePaths ) );

                ActionExitCode exitCode = ActionExitCode.Success; // Track the success status
                foreach ( string sourcePath in sourcePaths )
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

                        ( int childExitCode, string output, string error )
                            = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                thisProgram.FullName,
                                noAdmin: MainConfig.NoAdmin
                            );

                        _ = Logger.LogVerboseAsync( output + Environment.NewLine + error );
                        if ( childExitCode == 0 )
                            continue;

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
        [NotNull]
        private List<string> VerifyInstall( [ItemNotNull] List<string> sourcePaths = null )
        {
            if ( sourcePaths == null )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths == null )
                throw new ArgumentNullException( nameof( sourcePaths ) );

            foreach ( string sourcePath in sourcePaths )
            {
                string tslPatcherDirPath = Path.GetDirectoryName( sourcePath )
                    ?? throw new DirectoryNotFoundException(
                        $"Could not retrieve parent directory of '{sourcePath}'."
                    );

                //PlaintextLog=0
                string fullInstallLogFile = Path.Combine( tslPatcherDirPath, path2: "installlog.rtf" );
                if ( !File.Exists( fullInstallLogFile ) )
                {
                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirPath, path2: "installlog.txt" );
                    if ( !File.Exists( fullInstallLogFile ) )
                    {
                        throw new FileNotFoundException( message: "Install log file not found.", fullInstallLogFile );
                    }
                }

                string installLogContent = File.ReadAllText( fullInstallLogFile );

                return installLogContent.Split( Environment.NewLine.ToCharArray() ).Where( thisLine => thisLine.Contains( "Error: " ) || thisLine.Contains( "[Error]" ) ).ToList();
            }

            Logger.LogVerbose( "No errors found in TSLPatcher installation log file" );
            return new List<string>();
        }

        // this method removes the tslpatcher log file.
        // should be called BEFORE any tslpatcher install takes place from KOTORModSync, never post-install.
        public void CleanupTSLPatcherInstall( [ItemNotNull] List<string> sourcePaths = null )
        {
            if ( sourcePaths == null )
                sourcePaths = RealSourcePaths;
            if ( sourcePaths == null )
                throw new ArgumentNullException( nameof( sourcePaths ) );

            Logger.LogVerbose( "Preparing TSLPatcher install..." );
            foreach ( string sourcePath in sourcePaths )
            {
                string tslPatcherDirPath = Path.GetDirectoryName( sourcePath )
                    ?? throw new DirectoryNotFoundException(
                        $"Could not retrieve parent directory of '{sourcePath}'."
                    );

                //PlaintextLog=0
                string fullInstallLogFile = Path.Combine( tslPatcherDirPath, path2: "installlog.rtf" );

                if ( !File.Exists( fullInstallLogFile ) )
                {
                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirPath, path2: "installlog.txt" );
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

        private void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );

        private bool SetField<T>(
            [CanBeNull] ref T field,
            [CanBeNull] T value,
            [CallerMemberName] string propertyName = null
        )
        {
            if ( EqualityComparer<T>.Default.Equals( field, value ) )
                return false;

            field = value;
            OnPropertyChanged( propertyName );
            return true;
        }

        [NotNull]
        [ItemNotNull]
        public List<Option> GetChosenOptions() => _parentComponent?.Options
            .Where( x =>
                x != null
                && x.IsSelected
                && Source.Contains( x.Guid.ToString(), StringComparer.OrdinalIgnoreCase )
            ).ToList() ?? new List<Option>();
        /*return theseChosenOptions?.Count > 0
            ? theseChosenOptions
            : throw new KeyNotFoundException( message: "Could not find chosen option for this instruction" );*/
    }
}
