using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    [SuppressMessage( "ReSharper", "UnusedMember.Local" )]
    public static class FileHelper
    {
        [CanBeNull]
        public static string GetFolderName( string itemInArchivePath ) => Path.HasExtension( itemInArchivePath )
            ? Path.GetDirectoryName( itemInArchivePath )
            : itemInArchivePath;

        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        // use PlaintextLog=1
        public static void ReplaceLookupGameFolder( DirectoryInfo directory )
        {
            FileInfo[] iniFiles = directory.GetFiles( "*.ini", SearchOption.AllDirectories );
            if ( iniFiles.Length == 0 )
            {
                throw new InvalidOperationException( "No .ini files found!" );
            }

            foreach ( FileInfo file in iniFiles )
            {
                string filePath = file.FullName;
                string fileContents = File.ReadAllText( filePath );

                // Create a regular expression pattern to match "LookupGameFolder=1" with optional whitespace
                const string pattern = @"LookupGameFolder\s*=\s*1";

                // Use Regex.IsMatch to check if the pattern exists in the file contents
                if ( !Regex.IsMatch( fileContents, pattern ) )
                {
                    continue;
                }

                Logger.Log( $"Preventing tslpatcher automatic game lookups '{file.Name}'" );
                Logger.LogVerbose( $"change 'LookupGameFolder' from 1 to 0 in '{file.Name}'" );
                fileContents = Regex.Replace( fileContents, pattern, "LookupGameFolder=0" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }

        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        // use PlaintextLog=1
        public static void ReplacePlaintextLog( DirectoryInfo directory )
        {
            FileInfo[] iniFiles = directory.GetFiles( "*.ini", SearchOption.AllDirectories );
            if ( iniFiles.Length == 0 )
            {
                throw new InvalidOperationException( "No .ini files found!" );
            }

            foreach ( FileInfo file in iniFiles )
            {
                string filePath = file.FullName;
                string fileContents = File.ReadAllText( filePath );

                // Create a regular expression pattern to match "PlaintextLog=0" with optional whitespace
                const string pattern = @"PlaintextLog\s*=\s*0";

                // Use Regex.IsMatch to check if the pattern exists in the file contents
                if ( !Regex.IsMatch( fileContents, pattern, RegexOptions.IgnoreCase ) )
                {
                    continue;
                }

                Logger.Log( $"Redirecting TSLPatcher logging from '{file.Name}' to 'installlog.txt'" );
                Logger.LogVerbose( $"change 'PlaintextLog' from 0 to 1 in '{file.Name}'" );
                fileContents = Regex.Replace( fileContents, pattern, "PlaintextLog=1" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }

        public static async Task MoveFileAsync( string sourcePath, string destinationPath )
        {
            using ( var sourceStream = new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       4096,
                       true
                   ) )
            {
                using ( var destinationStream = new FileStream(
                           destinationPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           true
                       ) )
                {
                    await sourceStream.CopyToAsync( destinationStream );
                }
            }

            // The file is closed at this point, so it can be safely deleted
            File.Delete( sourcePath );
        }

        public static List<string> EnumerateFilesWithWildcards
            ( IEnumerable<string> filesAndFolders, bool topLevelOnly = false )
        {
            var result = new List<string>();
            var uniquePaths = new HashSet<string>( filesAndFolders );

            foreach ( string path in uniquePaths.Where( path => !string.IsNullOrEmpty( path ) ) )
            {
                try
                {
                    string formattedPath = Serializer.FixPathFormatting( path );

                    if ( !ContainsWildcards( formattedPath ) )
                    {
                        // Handle non-wildcard paths
                        if ( File.Exists( formattedPath ) )
                        {
                            result.Add( formattedPath );
                            continue;
                        }

                        if ( !Directory.Exists( formattedPath ) )
                        {
                            continue;
                        }

                        IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                            formattedPath,
                            "*",
                            topLevelOnly
                                ? SearchOption.TopDirectoryOnly
                                : SearchOption.AllDirectories
                        );

                        result.AddRange( matchingFiles );
                        continue;
                    }

                    // Handle wildcard paths
                    string directory = Path.GetDirectoryName( formattedPath );

                    if ( !string.IsNullOrEmpty( directory )
                        && directory.IndexOfAny( Path.GetInvalidPathChars() ) != -1
                        && Directory.Exists( directory ) )
                    {
                        IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                            directory,
                            Path.GetFileName( formattedPath ),
                            topLevelOnly
                                ? SearchOption.TopDirectoryOnly
                                : SearchOption.AllDirectories
                        );

                        result.AddRange( matchingFiles );
                        continue;
                    }

                    // Handle wildcard paths
                    string currentDirectory = formattedPath;

                    while ( ContainsWildcards( currentDirectory ) )
                    {
                        string parentDirectory = Path.GetDirectoryName( currentDirectory );
                        if ( string.IsNullOrEmpty( parentDirectory )
                            || parentDirectory == currentDirectory )
                        {
                            break; // Exit the loop if no parent directory is found or if the parent directory is the same as the current directory
                        }

                        currentDirectory = parentDirectory;
                    }

                    if ( string.IsNullOrEmpty( currentDirectory )
                        || !Directory.Exists( currentDirectory ) )
                    {
                        continue;
                    }

                    IEnumerable<string> checkFiles = Directory.EnumerateFiles(
                        currentDirectory,
                        "*",
                        topLevelOnly
                            ? SearchOption.TopDirectoryOnly
                            : SearchOption.AllDirectories
                    );

                    result.AddRange( checkFiles.Where( thisFile => WildcardPathMatch( thisFile, formattedPath ) ) );
                }
                catch ( Exception ex )
                {
                    // Handle or log the exception as required
                    Console.WriteLine( $"An error occurred while processing path '{path}': {ex.Message}" );
                }
            }

            return result;
        }

        private static bool ContainsWildcards( string path ) => path.Contains( '*' ) || path.Contains( '?' );

        public static bool WildcardPathMatch( string input, string patternInput )
        {
            // Fix path formatting
            input = Serializer.FixPathFormatting( input );
            patternInput = Serializer.FixPathFormatting( patternInput );

            // Split the input and pattern into directory levels
            string[] inputLevels = input.Split( Path.DirectorySeparatorChar );
            string[] patternLevels = patternInput.Split( Path.DirectorySeparatorChar );

            // Ensure the number of levels match
            if ( inputLevels.Length != patternLevels.Length )
            {
                return false;
            }

            // Iterate over each level and perform wildcard matching
            for ( int i = 0; i < inputLevels.Length; i++ )
            {
                string inputLevel = inputLevels[i];
                string patternLevel = patternLevels[i];

                // Check if the current level is a wildcard
                if ( patternLevel == "*"
                    || patternLevel == "?" )
                {
                    continue;
                }

                // Check if the current level matches the pattern
                if ( !WildcardMatch( inputLevel, patternLevel ) )
                {
                    return false;
                }
            }

            return true;
        }

        // Most end users don't know Regex, this function will convert basic wildcards to regex patterns.
        private static bool WildcardMatch( string input, string pattern )
        {
            // Escape special characters in the pattern
            pattern = Regex.Escape( pattern );

            // Replace * with .* and ? with . in the pattern
            pattern = pattern.Replace( @"\*", ".*" ).Replace( @"\?", "." );

            // Use regex to perform the wildcard matching
            return Regex.IsMatch( input, $"^{pattern}$" );
        }

        public static bool IsDirectoryWithName
            ( [NotNull] object directory, [NotNull] string name ) => directory is Dictionary<string, object> dict
            && dict.ContainsKey( "Name" )
            && dict["Name"] is string directoryName
            && directoryName.Equals( name, StringComparison.OrdinalIgnoreCase );

        [NotNull]
        private static Dictionary<string, object> CreateNewDirectory
            ( [CanBeNull] string name, bool isDirectory ) => new Dictionary<string, object>
        {
            { "Name", name },
            {
                "Type", isDirectory
                    ? "directory"
                    : "file"
            },
            { "Contents", new List<object>() }
        };
    }
}
