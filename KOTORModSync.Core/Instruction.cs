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
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Data;
using KOTORModSync.Core.FileSystemPathing;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

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

        [JsonProperty(nameof( Action ))]
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

            DirectoryInfo thisDestination = PathHelper.TryGetValidDirectoryInfo( destinationPath );
            if ( !thisDestination?.Exists == true )
            {
                thisDestination = PathHelper.GetCaseSensitivePath( thisDestination );
                if ( !noValidate && thisDestination is null )
                    throw new DirectoryNotFoundException( "Could not find the 'Destination' path!" );

                if ( thisDestination is null )
                    return;
            }

            RealDestinationPath = thisDestination;
        }

        // ReSharper disable once AssignNullToNotNullAttribute
        public async Task<ActionExitCode> ExtractFileAsync(DirectoryInfo argDestinationPath = null, [NotNull][ItemNotNull] List<string> argSourcePaths = null)
        {
            try
            {
                RealSourcePaths = argSourcePaths ?? RealSourcePaths ?? throw new ArgumentNullException(nameof(argSourcePaths));
                
                var semaphore = new SemaphoreSlim(initialCount: 4, maxCount: Environment.ProcessorCount * 4);
                ActionExitCode exitCode = ActionExitCode.Success;

                var cts = new CancellationTokenSource();
                async Task InnerExtractFileAsync(string sourcePath, CancellationToken cancellationToken)
                {
                    await semaphore.WaitAsync(cancellationToken); // Wait for a semaphore slot

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var thisFile = new FileInfo( sourcePath );
                        string sourceRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, sourcePath );
                        argDestinationPath = argDestinationPath
                            ?? thisFile.Directory
                            ?? throw new ArgumentNullException( nameof( argDestinationPath ) );

                        _ = Logger.LogAsync( $"Using archive path: '{sourceRelDirPath}'" );

                        if ( !ArchiveHelper.IsArchive( thisFile ) )
                        {
                            if ( !( _parentComponent is null ) )
                                _ = Logger.LogAsync( $"[Error] '{_parentComponent.Name}' failed to extract file '{thisFile.Name}'. Invalid archive?" );

                            exitCode = ActionExitCode.InvalidArchive;
                            return;
                        }

                        // (attempt to) handle self-extracting executable archives (7zip)
                        if ( thisFile.Extension.Equals( ".exe", StringComparison.OrdinalIgnoreCase ) )
                        {
                            (int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                thisFile.FullName,
                                $" -o\"{thisFile.DirectoryName}\" -y",
                                noAdmin: MainConfig.NoAdmin
                            );

                            if ( result.Item1 == 0 )
                                return;

                            exitCode = ActionExitCode.InvalidSelfExtractingExecutable;
                            throw new InvalidOperationException(
                                $"'{sourceRelDirPath}' is not a self-extracting executable as previously assumed. Cannot extract."
                            );
                        }

                        using ( FileStream stream = File.OpenRead( thisFile.FullName ) )
                        {
                            IArchive archive = null;

                            switch ( thisFile.Extension.ToLowerInvariant() )
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
                                throw new InvalidOperationException( $"Unable to parse archive '{sourceRelDirPath}'" );
                            }

                            IReader reader = archive.ExtractAllEntries();
                            while ( reader.MoveToNextEntry() )
                            {
                                if ( reader.Entry.IsDirectory )
                                    continue;

                                string extractFolderName = Path.GetFileNameWithoutExtension( thisFile.Name );
                                string destinationItemPath = Path.Combine(
                                    argDestinationPath.FullName,
                                    extractFolderName,
                                    reader.Entry.Key
                                );
                                var destinationDirectory = new InsensitivePath(Path.GetDirectoryName( destinationItemPath ), isFile:false);
                                string destinationRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, destinationDirectory.FullName );

                                if ( !Directory.Exists( destinationDirectory.FullName ) && destinationDirectory.IsFile != true )
                                {
                                    _ = Logger.LogAsync( $"Create directory '{destinationRelDirPath}'" );
                                    _ = Directory.CreateDirectory( destinationDirectory.FullName );
                                }

                                _ = Logger.LogAsync( $"Extract '{reader.Entry.Key}' to '{destinationRelDirPath}'" );

                                try
                                {
                                    await Task.Run(
                                        () => reader.WriteEntryToDirectory(
                                            destinationDirectory.FullName,
                                            ArchiveHelper.DefaultExtractionOptions
                                        ),
                                        cancellationToken
                                    );
                                }
                                catch ( UnauthorizedAccessException )
                                {
                                    _ = Logger.LogWarningAsync( $"Skipping file '{reader.Entry.Key}' due to lack of permissions." );
                                    //exitCode = ActionExitCode.UnauthorizedAccessException;
                                }
                                catch ( Exception ex )
                                {
                                    _ = Logger.LogWarningAsync($"Falling back to 7-Zip for '{reader.Entry.Key}' due to an error: {ex.Message}.");
                                    cts.Cancel();
                                }
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        _ = Logger.LogWarningAsync($"Falling back to 7-Zip for entire archive due to an error: {ex.Message}.");
                        cts.Cancel();
                        throw new OperationCanceledException();
                    }
                    finally
                    {
                        _ = semaphore.Release(); // Release the semaphore slot
                    }
                }
                
                var extractionTasks = RealSourcePaths.Select(sourcePath => InnerExtractFileAsync(sourcePath, cts.Token)).ToList();

                try
                {
                    await Task.WhenAll(extractionTasks); // Wait for all extraction tasks to complete
                }
                catch (OperationCanceledException)
                {
                    // Restarting all tasks using ArchiveHelper.ExtractWith7Zip
                    try
                    {
                        var fallbackTasks = RealSourcePaths.Select(sourcePath =>
                        {
                            var thisFile = new FileInfo(sourcePath);
                            using (FileStream stream = File.OpenRead(thisFile.FullName))
                            {
                                // ReSharper disable once PossibleNullReferenceException
                                var destinationDirectory = new InsensitivePath(
                                    Path.Combine(
                                        argDestinationPath?.FullName ?? thisFile.Directory.FullName,
                                        Path.GetFileNameWithoutExtension( thisFile.Name )
                                    ),
                                    isFile: false
                                );
                                
                                string destinationRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, destinationDirectory.FullName );
                                if ( !destinationDirectory.Exists || destinationDirectory.IsFile != true )
                                {
                                    _ = Logger.LogAsync( $"Create directory '{destinationRelDirPath}'" );
                                    _ = Directory.CreateDirectory( destinationDirectory.FullName );
                                }
                                ArchiveHelper.ExtractWith7Zip(stream, destinationDirectory.FullName);
                            }
                            return Task.CompletedTask;
                        }).ToList();

                        await Task.WhenAll(fallbackTasks);
                    }
                    catch ( Exception ex )
                    {
                        await Logger.LogExceptionAsync( ex );
                        exitCode = ActionExitCode.ArchiveParseError;
                    }
                }

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

            FileInfo[] files = directoryPath.GetFilesSafely();
            Dictionary<string, int> fileNameCounts = caseInsensitive
                ? new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase )
                : new Dictionary<string, int>();

            foreach ( FileInfo fileInfo in files )
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( fileInfo.Name );
                string thisExtension = fileInfo.Extension;

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

            foreach ( FileInfo thisFile in files )
            {
                if ( !ShouldDeleteFile( thisFile ) )
                    continue;

                try
                {
                    thisFile.Delete();
                    Logger.Log( $"Deleted file: '{thisFile}'" );
                    Logger.LogVerbose( $"Leaving alone '{fileNameCounts[Path.GetFileNameWithoutExtension( thisFile.Name )]-1}' files with the same name of '{Path.GetFileNameWithoutExtension( thisFile.Name )}'." );
                }
                catch ( Exception ex )
                {
                    Logger.LogException( ex );
                }
            }

            bool ShouldDeleteFile( FileSystemInfo fileSystemInfoItem )
            {
                string fileName = fileSystemInfoItem.Name;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension( fileName );
                string fileExtensionFromFile = fileSystemInfoItem.Extension;

                if ( string.IsNullOrEmpty( fileNameWithoutExtension ) )
                {
                    Logger.LogVerbose( $"Conditional 1: fileNameWithoutExtension from '{fileName}' is null or empty" );
                }
                else if ( !fileNameCounts.ContainsKey( fileNameWithoutExtension ) )
                {
                    /*Logger.LogVerbose(
                        $"Conditional 2: '{fileNameWithoutExtension}' is not present in '{fileNameCounts}' ergo not an extension included for this deletion."
                    );*/
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
                // ReSharper disable once PossibleNullReferenceException
                foreach ( string thisFilePath in sourcePaths )
                {
                    string sourceRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, thisFilePath );
                    var thisFile = new InsensitivePath( thisFilePath, isFile:true );

                    if ( !Path.IsPathRooted( thisFile.FullName ) || !thisFile.Exists )
                    {
                        var ex = new ArgumentNullException(
                            $"Invalid wildcards or file does not exist: '{sourceRelDirPath}'"
                        );
                        Logger.LogException( ex );
                        return ActionExitCode.FileNotFoundPost;
                    }

                    // Delete the file synchronously
                    try
                    {
                        thisFile.Delete();
                        Logger.Log( $"Deleting '{sourceRelDirPath}'..." );
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
                // ReSharper disable once PossibleNullReferenceException
                foreach ( string sourcePath in sourcePaths )
                {
                    string fileName = Path.GetFileName( sourcePath );
                    string sourceRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, sourcePath );
                    // Check if the source file already exists
                    if ( !File.Exists( sourcePath ) )
                    {
                        Logger.Log( $"'{sourceRelDirPath}' does not exist!" );
                        exitCode = ActionExitCode.FileNotFoundPost;
                        continue;
                    }

                    // Check if the destination file already exists
                    string destinationFilePath = Path.Combine(
                        Path.GetDirectoryName( sourcePath ) ?? string.Empty,
                        Destination
                    );
                    string destinationRelDirPath = PathHelper.GetRelativePath( MainConfig.DestinationPath.FullName, destinationFilePath );
                    if ( File.Exists( destinationFilePath ) )
                    {
                        if ( !Overwrite )
                        {
                            exitCode = ActionExitCode.RenameTargetAlreadyExists;
                            Logger.Log(
                                $"File '{fileName}' already exists in {Path.GetDirectoryName( destinationRelDirPath )},"
                                + $" skipping file. Reason: Overwrite set to False )"
                            );

                            continue;
                        }

                        Logger.Log( $"Removing pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True" );
                        File.Delete( destinationFilePath );
                    }

                    // Move the file
                    try
                    {
                        Logger.Log( $"Rename '{sourceRelDirPath}' to '{destinationRelDirPath}'" );
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

        public async Task<ActionExitCode> CopyFileAsync(
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null,
            DirectoryInfo destinationPath = null
        )
        {
            if (sourcePaths.IsNullOrEmptyCollection())
                sourcePaths = RealSourcePaths;
            if (sourcePaths.IsNullOrEmptyCollection())
                throw new ArgumentNullException(nameof(sourcePaths));

            if (destinationPath?.Exists != true)
                destinationPath = RealDestinationPath;
            if (destinationPath?.Exists != true)
                throw new ArgumentNullException(nameof(destinationPath));
            
            int initialCount = MainConfig.UseMultiThreadedIO ? 8 : 1;
            int maxCount = MainConfig.UseMultiThreadedIO ? 16 : 1;
            var semaphore = new SemaphoreSlim(initialCount, maxCount);

            async Task CopyIndividualFileAsync(string sourcePath)
            {
                await semaphore.WaitAsync(); // Wait for a semaphore slot
                try
                {
                    string sourceRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, sourcePath );
                    string fileName = Path.GetFileName(sourcePath);
                    string destinationFilePath = MainConfig.CaseInsensitivePathing
                        ? PathHelper.GetCaseSensitivePath(Path.Combine(destinationPath.FullName, fileName), isFile: true).Item1
                        : Path.Combine( destinationPath.FullName, fileName );
                    string destinationRelDirPath = PathHelper.GetRelativePath( MainConfig.DestinationPath.FullName, destinationFilePath );

                    // Check if the destination file already exists
                    if (File.Exists(destinationFilePath))
                    {
                        if (!Overwrite)
                        {
                            await Logger.LogWarningAsync(
                                $"File '{fileName}' already exists in {Path.GetDirectoryName( destinationRelDirPath )},"
                                + " skipping file. Reason: Overwrite set to False )"
                            );

                            return;
                        }

                        await Logger.LogAsync($"File '{fileName}' already exists in {Path.GetDirectoryName( destinationRelDirPath )},"
                            + $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True");
                        File.Delete(destinationFilePath);
                    }

                    await Logger.LogAsync($"Copy '{sourceRelDirPath}' to '{destinationRelDirPath}'");
                    File.Copy(sourcePath, destinationFilePath);
                }
                catch (Exception ex)
                {
                    await Logger.LogExceptionAsync(ex);
                    throw;
                }
                finally
                {
                    _ = semaphore.Release(); // Release the semaphore slot
                }
            }

            if ( sourcePaths is null )
                throw new NullReferenceException(nameof( sourcePaths ));

            var tasks = sourcePaths.Select(CopyIndividualFileAsync).ToList();

            try
            {
                await Task.WhenAll(tasks); // Wait for all move tasks to complete
                return ActionExitCode.Success;
            }
            catch
            {
                return ActionExitCode.UnknownError;
            }
        }

        public async Task<ActionExitCode> MoveFileAsync(
            // ReSharper disable once AssignNullToNotNullAttribute
            [ItemNotNull][NotNull] List<string> sourcePaths = null,
            DirectoryInfo destinationPath = null
        )
        {
            if (sourcePaths.IsNullOrEmptyCollection())
                sourcePaths = RealSourcePaths;
            if (sourcePaths.IsNullOrEmptyCollection())
                throw new ArgumentNullException(nameof(sourcePaths));

            if (destinationPath?.Exists != true)
                destinationPath = RealDestinationPath;
            if (destinationPath?.Exists != true)
                throw new ArgumentNullException(nameof(destinationPath));
            
            int initialCount = MainConfig.UseMultiThreadedIO ? 8 : 1;
            int maxCount = MainConfig.UseMultiThreadedIO ? 16 : 1;
            var semaphore = new SemaphoreSlim(initialCount, maxCount);

            async Task MoveIndividualFileAsync(string sourcePath)
            {
                await semaphore.WaitAsync(); // Wait for a semaphore slot

                try
                {
                    string sourceRelDirPath = PathHelper.GetRelativePath( MainConfig.SourcePath.FullName, sourcePath );
                    string fileName = Path.GetFileName(sourcePath);
                    string destinationFilePath = MainConfig.CaseInsensitivePathing
                        ? PathHelper.GetCaseSensitivePath(Path.Combine(destinationPath.FullName, fileName), isFile: true).Item1
                        : Path.Combine( destinationPath.FullName, fileName );
                    string destinationRelDirPath = PathHelper.GetRelativePath( MainConfig.DestinationPath.FullName, destinationFilePath );

                    // Check if the destination file already exists
                    if (File.Exists(destinationFilePath))
                    {
                        if (!Overwrite)
                        {
                            await Logger.LogWarningAsync(
                                $"File '{fileName}' already exists in {Path.GetDirectoryName( destinationRelDirPath )},"
                                + " skipping file. Reason: Overwrite set to False )"
                            );

                            return;
                        }
                        
                        await Logger.LogAsync($"File '{fileName}' already exists in {Path.GetDirectoryName( destinationRelDirPath )},"
                            + $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True");
                        File.Delete(destinationFilePath);
                    }

                    await Logger.LogAsync($"Move '{sourceRelDirPath}' to '{destinationRelDirPath}'");
                    File.Move(sourcePath, destinationFilePath);
                }
                catch (Exception ex)
                {
                    await Logger.LogExceptionAsync(ex);
                    throw;
                }
                finally
                {
                    _ = semaphore.Release(); // Release the semaphore slot
                }
            }

            if ( sourcePaths is null )
                throw new NullReferenceException(nameof( sourcePaths ));

            var tasks = sourcePaths.Select(MoveIndividualFileAsync).ToList();

            try
            {
                await Task.WhenAll(tasks); // Wait for all move tasks to complete
                return ActionExitCode.Success;
            }
            catch
            {
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
                    DirectoryInfo tslPatcherDirectory = File.Exists( t )
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
                        File.Delete( fullInstallLogFile );
                    }
                    //PlaintextLog=1
                    fullInstallLogFile = Path.Combine( tslPatcherDirectory.FullName, path2: "installlog.txt" );
                    if ( File.Exists( fullInstallLogFile ) )
                        File.Delete( fullInstallLogFile );
                    
                    IniHelper.ReplacePlaintextLog( tslPatcherDirectory );
                    await Logger.LogVerboseAsync( " - Pre-installing TSLPatcher LookUpGameFolder hooks..." );
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
                        await Logger.LogAsync( $"Using CLI to run command: '{tslPatcherCliPath} {args}'" );

                    // ReSharper disable twice UnusedVariable
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
                        ( int childExitCode, string output, string error )
                            = await PlatformAgnosticMethods.ExecuteProcessAsync(
                                sourcePath,
                                noAdmin: MainConfig.NoAdmin,
                                cmdlineArgs: Utility.Utility.ReplaceCustomVariables( Arguments )
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

                return installLogContent
                    .Split( Environment.NewLine.ToCharArray() )
                    .Where( thisLine => thisLine.Contains( "Error: " ) || thisLine.Contains( "[Error]" ) )
                    .ToList();
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
