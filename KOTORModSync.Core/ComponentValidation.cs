using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;

namespace KOTORModSync.Core
{
    public class ComponentValidation
    {
        public enum ArchivePathCode
        {
            NotAnArchive,
            PathMissingArchiveName,
            CouldNotOpenArchive,
            NotFoundInArchive,
            FoundSuccessfully,
            NeedsAppendedArchiveName,
            NoArchivesFound
        }

        private readonly List<ValidationResult> _validationResults;
        public readonly Component Component;
        public readonly List<Component> ComponentsList;

        public ComponentValidation( Component component, List<Component> componentsList )
        {
            Component = component;
            ComponentsList = componentsList;
            _validationResults = new List<ValidationResult>();
        }

        public bool Run() =>
            // Verify all the instructions' paths line up with hierarchy of the archives
            VerifyExtractPaths( Component )
            // Ensure all the 'Destination' keys are valid for their respective action.
            && ParseDestinationWithAction( Component );

        private void AddError( string message, Instruction instruction ) =>
            _validationResults.Add( new ValidationResult( this, instruction, message, true ) );

        private void AddWarning( string message, Instruction instruction ) =>
            _validationResults.Add( new ValidationResult( this, instruction, message, false ) );

        public List<string> GetErrors() =>
            _validationResults.Where( r => r.IsError ).Select( r => r.Message ).ToList();

        public List<string> GetErrors( int instructionIndex ) =>
            _validationResults.Where( r => r.InstructionIndex == instructionIndex && r.IsError )
                .Select( r => r.Message )
                .ToList();

        public List<string> GetErrors( Instruction instruction ) =>
            _validationResults.Where( r => r.Instruction == instruction && r.IsError )
                .Select( r => r.Message )
                .ToList();

        public List<string> GetWarnings() =>
            _validationResults.Where( r => !r.IsError ).Select( r => r.Message ).ToList();

        public List<string> GetWarnings( int instructionIndex ) =>
            _validationResults.Where( r => r.InstructionIndex == instructionIndex && !r.IsError )
                .Select( r => r.Message )
                .ToList();

        public List<string> GetWarnings( Instruction instruction ) =>
            _validationResults.Where( r => r.Instruction == instruction && !r.IsError )
                .Select( r => r.Message )
                .ToList();

        public bool VerifyExtractPaths( Component component )
        {
            try
            {
                bool success = true;

                // Confirm that all Dependencies are found in either InstallBefore and InstallAfter:
                List<string> allArchives = GetAllArchivesFromInstructions( component );

                // probably something wrong if there's no archives found.
                if ( allArchives.Count == 0 )
                {
                    foreach ( Instruction instruction in component.Instructions )
                    {
                        if ( !instruction.Action.Equals( "extract", StringComparison.OrdinalIgnoreCase ) )
                        {
                            continue;
                        }

                        AddError(
                            $"Missing Required Archives for 'Extract' action: [{string.Join( ",", instruction.Source )}]",
                            instruction
                        );
                        success = false;
                    }

                    return success;
                }

                foreach ( Instruction instruction in component.Instructions )
                {
                    // we already checked if the archive exists in GetAllArchivesFromInstructions.
                    if ( instruction.Action.Equals( "extract", StringComparison.OrdinalIgnoreCase ) )
                    {
                        continue;
                    }

                    bool archiveNameFound = true;
                    if ( instruction.Source == null )
                    {
                        AddWarning( "Instruction does not have a 'Source' key defined", instruction );
                        success = false;
                        continue;
                    }

                    for ( int index = 0; index < instruction.Source.Count; index++ )
                    {
                        string sourcePath = Serializer.FixPathFormatting( instruction.Source[index] );

                        // todo
                        if ( sourcePath.StartsWith( "<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase ) )
                        {
                            continue;
                        }

                        // ensure tslpatcher.exe sourcePaths use the action 'tslpatcher'
                        if ( sourcePath.EndsWith( "tslpatcher.exe", StringComparison.OrdinalIgnoreCase )
                            && !instruction.Action.Equals( "tslpatcher", StringComparison.OrdinalIgnoreCase ) )
                        {
                            AddWarning(
                                "'tslpatcher.exe' used in Source path without the action 'tslpatcher', was this intentional?",
                                instruction
                            );
                        }

                        (bool, bool) result = IsSourcePathInArchives( sourcePath, allArchives, instruction );

                        // For some unholy reason, some archives act like there's another top level folder named after the archive to extract.
                        // doesn't even seem to be related to the archive type. Can't reproduce in 7zip either.
                        // either way, this will hide the issue until a real solution comes along.
                        if ( !result.Item1
                            && MainConfig.AttemptFixes )
                        {
                            // Split the directory name using the directory separator character
                            string[] parts = sourcePath.Split( Path.DirectorySeparatorChar );

                            // Add the first part of the path and repeat it at the beginning
                            // i.e. archive/my/custom/path becomes archive/archive/my/custom/path
                            string duplicatedPart = parts[1] + Path.DirectorySeparatorChar + parts[1];
                            string[] remainingParts = parts.Skip( 2 ).ToArray();

                            string path = string.Join(
                                Path.DirectorySeparatorChar.ToString(),
                                new[] { parts[0], duplicatedPart }.Concat( remainingParts )
                            );

                            result = IsSourcePathInArchives( path, allArchives, instruction );
                            if ( result.Item1 )
                            {
                                _ = Logger.LogAsync( "Fixing the above issue automatically..." );
                                instruction.Source[index] = path;
                            }
                        }

                        success &= result.Item1;
                        archiveNameFound &= result.Item2;
                    }

                    if ( !archiveNameFound )
                    {
                        AddWarning(
                            "'Source' path does not include the archive's name as part"
                            + " of the extraction folder, possible FileNotFound exception.",
                            instruction
                        );
                    }
                }

                return success;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        public List<string> GetAllArchivesFromInstructions( Component component )
        {
            var allArchives = new List<string>();

            foreach ( Instruction instruction in component.Instructions )
            {
                if ( instruction.Source == null
                    || instruction.Action != "extract" )
                {
                    continue;
                }

                List<string> realPaths = FileHelper.EnumerateFilesWithWildcards(
                    instruction.Source.ConvertAll( Utility.Utility.ReplaceCustomVariables ),
                    true
                );
                foreach ( string realSourcePath in realPaths )
                {
                    if ( Path.GetExtension( realSourcePath ).Equals( ".exe", StringComparison.OrdinalIgnoreCase ) )
                    {
                        allArchives.Add( realSourcePath );
                        continue; // no way to verify self-extracting executables.
                    }

                    if ( !ArchiveHelper.IsArchive( realSourcePath ) )
                    {
                        AddWarning(
                            $"Archive '{Path.GetFileName( realSourcePath )}'"
                            + " is referenced in a non 'extract' action. Was this intentional?",
                            instruction
                        );
                        continue;
                    }

                    if ( File.Exists( realSourcePath ) )
                    {
                        allArchives.Add( realSourcePath );
                        continue;
                    }

                    if ( !Component.ShouldRunInstruction( instruction, ComponentsList ) )
                    {
                        continue;
                    }

                    AddError(
                        "Missing required download:" + $" '{Path.GetFileName( realSourcePath )}'",
                        instruction
                    );

                    continue;
                }
            }

            return allArchives;
        }

        public bool ParseDestinationWithAction( Component component )
        {
            bool success = true;
            foreach ( Instruction instruction in component.Instructions )
            {
                switch ( instruction.Action )
                {
                    case null:
                        continue;
                    // tslpatcher must always use <<kotorDirectory>> and nothing else.
                    case "tslpatcher" when instruction.Destination == null:
                        instruction.Destination = "<<kotorDirectory>>";
                        break;

                    case "tslpatcher" when !instruction.Destination.Equals(
                        "<<kotorDirectory>>",
                        StringComparison.OrdinalIgnoreCase
                    ):
                        success = false;
                        AddError(
                            "'Destination' key must be either null or string literal '<<kotorDirectory>>'"
                            + $" for this action. Got '{instruction.Destination}'",
                            instruction
                        );
                        if ( MainConfig.AttemptFixes )
                        {
                            Logger.Log( "Fixing the above issue automatically." );
                            instruction.Destination = "<<kotorDirectory>>";
                        }

                        break;
                    // extract and delete cannot use the 'Destination' key.
                    case "extract":
                    case "delete":
                        if ( instruction.Destination == null )
                        {
                            break;
                        }

                        success = false;
                        AddError(
                            "'Destination' key cannot be used with this action." + $" Got '{instruction.Destination}'",
                            instruction
                        );

                        if ( !MainConfig.AttemptFixes )
                        {
                            break;
                        }

                        Logger.Log( "Fixing the above issue automatically." );
                        instruction.Destination = null;

                        break;
                    // rename should never use <<kotorDirectory>>\\Override
                    case "rename":
                        if ( instruction.Destination?.Equals(
                                $"<<kotorDirectory>>{Path.DirectorySeparatorChar}Override",
                                StringComparison.Ordinal
                            )
                            != false )
                        {
                            success = false;
                            AddError(
                                "Incorrect 'Destination' format."
                                + $" Got '{instruction.Destination}',"
                                + " expected a filename.",
                                instruction
                            );
                        }

                        break;
                    default:

                        string destinationPath = string.Empty;
                        if ( instruction.Destination != null )
                        {
                            destinationPath = Utility.Utility.ReplaceCustomVariables( instruction.Destination );
                        }

                        if ( string.IsNullOrWhiteSpace( destinationPath )
                            || destinationPath.Any( c => Path.GetInvalidPathChars().Contains( c ) )
                            || !Directory.Exists( destinationPath ) )
                        {
                            success = false;
                            AddError( "Destination cannot be found!" + $" Got '{destinationPath}'", instruction );

                            /*if ( !MainConfig.AttemptFixes )
                            {
                                break;
                            }

                            Logger.Log(
                                "Fixing the above issue automatically"
                                + $" (setting Destination to '<<kotorDirectory>>{Path.DirectorySeparatorChar}Override')"
                            );
                            instruction.Destination = $"<<kotorDirectory>>{Path.DirectorySeparatorChar}Override";*/
                        }

                        break;
                }
            }

            return success;
        }

        private static string GetErrorDescription( ArchivePathCode code )
        {
            switch ( code )
            {
                case ArchivePathCode.FoundSuccessfully:
                    return "File successfully found in archive.";
                case ArchivePathCode.NotAnArchive:
                    return "Not an archive";
                case ArchivePathCode.PathMissingArchiveName:
                    return "Missing archive name in path";
                case ArchivePathCode.CouldNotOpenArchive:
                    return "Could not open archive";
                case ArchivePathCode.NotFoundInArchive:
                    return "Not found in archive";
                case ArchivePathCode.NoArchivesFound:
                    return "No archives found/no extract instructions created";
                default:
                    return "Unknown error";
            }
        }

        public (bool, bool) IsSourcePathInArchives
            ( string sourcePath, List<string> allArchives, Instruction instruction )
        {
            bool foundInAnyArchive = false;
            bool hasError = false;
            bool archiveNameFound = false;
            string errorDescription = string.Empty;

            sourcePath = Serializer.FixPathFormatting( sourcePath )
                .Replace( $"<<modDirectory>>{Path.DirectorySeparatorChar}", "" )
                .Replace( $"<<kotorDirectory>>{Path.DirectorySeparatorChar}", "" );

            foreach ( string archivePath in allArchives )
            {
                // Check if the archive name matches the first portion of the sourcePath
                string archiveName = Path.GetFileNameWithoutExtension( archivePath );
                string[] pathParts = sourcePath.Split( Path.DirectorySeparatorChar );
                archiveNameFound = FileHelper.WildcardPathMatch( archiveName, pathParts[0] );

                ArchivePathCode code = IsPathInArchive( sourcePath, archivePath );

                if ( code == ArchivePathCode.FoundSuccessfully )
                {
                    foundInAnyArchive = true;
                    break;
                }

                if ( code == ArchivePathCode.NotFoundInArchive )
                {
                    continue;
                }

                hasError = true;
                errorDescription += GetErrorDescription( code ) + Environment.NewLine;
            }

            if ( hasError )
            {
                AddError( $"Invalid source path '{sourcePath}'. Reason: {errorDescription}", instruction );
                return (false, archiveNameFound);
            }

            if ( foundInAnyArchive )
            {
                return (true, true);
            }

            // todo, stop displaying errors for self extracting executables. This is the only mod using one that I've seen out of 200-some.
            if ( Component.Name.Equals( "Improved AI", StringComparison.OrdinalIgnoreCase ) )
            {
                return (true, true);
            }

            // archive not required if instruction isn't running.
            if ( !Component.ShouldRunInstruction( instruction, ComponentsList, false ) )
            {
                return (true, true);
            }

            AddError( $"Failed to find '{sourcePath}' in any archives!", instruction );
            return (false, archiveNameFound);
        }

        private static ArchivePathCode IsPathInArchive( string relativePath, string archivePath )
        {
            if ( !ArchiveHelper.IsArchive( archivePath ) )
            {
                return ArchivePathCode.NotAnArchive;
            }

            // todo: self-extracting 7z executables
            if ( Path.GetExtension( archivePath ) == ".exe" )
            {
                return ArchivePathCode.FoundSuccessfully;
            }

            using ( FileStream stream = File.OpenRead( archivePath ) )
            {
                IArchive archive = null;

                if ( archivePath.EndsWith( ".zip" ) )
                {
                    archive = SharpCompress.Archives.Zip.ZipArchive.Open( stream );
                }
                else if ( archivePath.EndsWith( ".rar" ) )
                {
                    archive = RarArchive.Open( stream );
                }
                else if ( archivePath.EndsWith( ".7z" ) )
                {
                    archive = SevenZipArchive.Open( stream );
                }

                if ( archive == null )
                {
                    return ArchivePathCode.CouldNotOpenArchive;
                }

                // everything is extracted to a new directory named after the archive.
                string archiveNameAppend = Path.GetFileNameWithoutExtension( archivePath );

                // if the Source key represents the top level extraction directory, check that first.
                if ( FileHelper.WildcardPathMatch( archiveNameAppend, relativePath ) )
                {
                    return ArchivePathCode.FoundSuccessfully;
                }

                var folderPaths = new HashSet<string>();

                foreach ( IArchiveEntry entry in archive.Entries )
                {
                    // Append extracted directory and ensure every slash is a backslash.
                    string itemInArchivePath = archiveNameAppend
                        + Path.DirectorySeparatorChar
                        + entry.Key.Replace( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );

                    // Some archives loop through folders while others don't.
                    // Check if itemInArchivePath has an extension to determine folderName.
                    string folderName = FileHelper.GetFolderName( itemInArchivePath );

                    // Add the folder path to the list, after removing trailing slashes.
                    if ( !string.IsNullOrEmpty( folderName ) )
                    {
                        _ = folderPaths.Add(
                            folderName.TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar )
                        );
                    }

                    // Check if itemInArchivePath matches relativePath using wildcard matching.
                    if ( FileHelper.WildcardPathMatch( itemInArchivePath, relativePath ) )
                    {
                        return ArchivePathCode.FoundSuccessfully;
                    }
                }

                // check if instruction.Source matches a folder.
                foreach ( string folderPath in folderPaths )
                {
                    if ( FileHelper.WildcardPathMatch( folderPath, relativePath ) )
                    {
                        return ArchivePathCode.FoundSuccessfully;
                    }
                }
            }

            return ArchivePathCode.NotFoundInArchive;
        }
    }
}
