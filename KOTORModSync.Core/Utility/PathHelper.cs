// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class PathHelper
    {
        [CanBeNull]
        public static string GetFolderName( [CanBeNull] string itemInArchivePath ) =>
            Path.HasExtension( itemInArchivePath )
                ? Path.GetDirectoryName( itemInArchivePath )
                : itemInArchivePath;

        [CanBeNull]
        public static string GetCaseSensitivePath([NotNull] string path)
        {
            if ( string.IsNullOrWhiteSpace( path ) )
                throw new ArgumentException( $"'{nameof( path )}' cannot be null or whitespace.", nameof( path ) );

            // Check for invalid characters in the path
            char[] invalidChars = Path.GetInvalidPathChars();
            if (path.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException( $"{path} is not a valid path!" );
            }

            path = Path.GetFullPath(path); // Get the full path to handle relative paths
            if ( File.Exists( path ) || Directory.Exists( path ) )
                return path;

            DirectoryInfo parentDir = Directory.GetParent(path);
            if (parentDir == null)
                return null;

            string searchName = Path.GetFileName(path);
            DirectoryInfo[] matchingDirectories = parentDir.GetDirectories(searchPattern: "*", SearchOption.TopDirectoryOnly);

            if (matchingDirectories.Length > 0)
            {
                foreach ( DirectoryInfo dir in matchingDirectories)
                {
                    if (dir.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
                        return dir.FullName;
                }
            }

            FileInfo[] matchingFiles = parentDir.GetFiles(searchPattern: "*", SearchOption.TopDirectoryOnly);

            if (matchingFiles.Length > 0)
            {
                foreach ( FileInfo file in matchingFiles)
                {
                    if (file.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
                        return file.FullName;
                }
            }

            return null;
        }

        [CanBeNull]
        public static string GetCaseSensitivePath( [CanBeNull] FileInfo file ) => GetCaseSensitivePath( file?.FullName );
        [CanBeNull]
        public static string GetCaseSensitivePath( [CanBeNull] DirectoryInfo directory ) => GetCaseSensitivePath( directory?.FullName );

        public static async Task MoveFileAsync( [NotNull] string sourcePath, [NotNull] string destinationPath )
        {
            if ( sourcePath is null )
                throw new ArgumentNullException( nameof( sourcePath ) );
            if ( destinationPath is null )
                throw new ArgumentNullException( nameof( destinationPath ) );

            using ( var sourceStream = new FileStream(
                       path: sourcePath,
                       mode: FileMode.Open,
                       access: FileAccess.Read,
                       share: FileShare.Read,
                       bufferSize: 4096,
                       useAsync: true
                   ) )
            {
                using ( var destinationStream = new FileStream(
                           path: destinationPath,
                           mode: FileMode.CreateNew,
                           access: FileAccess.Write,
                           share: FileShare.None,
                           bufferSize: 4096,
                           useAsync: true
                       ) )
                {
                    await sourceStream.CopyToAsync( destinationStream );
                }
            }

            // The file is closed at this point, so it can be safely deleted
            File.Delete( sourcePath );
        }

        [CanBeNull]
        [ItemNotNull]
        public static List<string> EnumerateFilesWithWildcards(
            [NotNull] IEnumerable<string> filesAndFolders,
            bool topLevelOnly = false
        )
        {
            if ( filesAndFolders is null )
                throw new ArgumentNullException( nameof( filesAndFolders ) );

            var result = new List<string>();
            var uniquePaths = new HashSet<string>( filesAndFolders );

            foreach ( string path in uniquePaths.Where( path => !string.IsNullOrEmpty( path ) ) )
            {
                try
                {
                    string formattedPath = FixPathFormatting( path );

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
                            searchPattern: "*",
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

                        // Exit the loop if no parent directory is found or if the parent directory is the same as the current directory
                        if ( string.IsNullOrEmpty( parentDirectory ) || parentDirectory == currentDirectory )
                            break;

                        currentDirectory = parentDirectory;
                    }

                    if ( string.IsNullOrEmpty( currentDirectory ) || !Directory.Exists( currentDirectory ) )
                    {
                        continue;
                    }

                    IEnumerable<string> checkFiles = Directory.EnumerateFiles(
                        currentDirectory,
                        searchPattern: "*",
                        topLevelOnly
                            ? SearchOption.TopDirectoryOnly
                            : SearchOption.AllDirectories
                    );

                    result.AddRange(
                        checkFiles.Where(
                            thisFile => !( thisFile is null ) && WildcardPathMatch( thisFile, formattedPath )
                        )
                    );
                }
                catch ( Exception ex )
                {
                    // Handle or log the exception as required
                    Console.WriteLine( $"An error occurred while processing path '{path}': {ex.Message}" );
                }
            }

            return result;
        }

        private static bool ContainsWildcards( [NotNull] string path ) => path.Contains( '*' ) || path.Contains( '?' );

        public static bool WildcardPathMatch( [NotNull] string input, [NotNull] string patternInput )
        {
            if ( input is null )
                throw new ArgumentNullException( nameof( input ) );
            if ( patternInput is null )
                throw new ArgumentNullException( nameof( patternInput ) );

            // Fix path formatting
            input = FixPathFormatting( input );
            patternInput = FixPathFormatting( patternInput );

            // Split the input and pattern into directory levels
            string[] inputLevels = input.Split( Path.DirectorySeparatorChar );
            string[] patternLevels = patternInput.Split( Path.DirectorySeparatorChar );

            // Ensure the number of levels match
            if ( inputLevels.Length != patternLevels.Length )
                return false;

            // Iterate over each level and perform wildcard matching
            for ( int i = 0; i < inputLevels.Length; i++ )
            {
                string inputLevel = inputLevels[i];
                string patternLevel = patternLevels[i];

                // Check if the current level is a wildcard
                if ( patternLevel == "*" || patternLevel == "?" )
                    continue;

                // Check if the current level matches the pattern
                if ( !WildcardMatch( inputLevel, patternLevel ) )
                    return false;
            }

            return true;
        }

        // Most end users don't know Regex, this function will convert basic wildcards to regex patterns.
        private static bool WildcardMatch( [NotNull] string input, [NotNull] string pattern )
        {
            if ( input is null )
                throw new ArgumentNullException( nameof( input ) );
            if ( pattern is null )
                throw new ArgumentNullException( nameof( pattern ) );

            // Escape special characters in the pattern
            pattern = Regex.Escape( pattern );

            // Replace * with .* and ? with . in the pattern
            pattern = pattern.Replace( oldValue: @"\*", newValue: ".*" )
                .Replace( oldValue: @"\?", newValue: "." );

            // Use regex to perform the wildcard matching
            return Regex.IsMatch( input, $"^{pattern}$" );
        }

        [NotNull]
        public static string FixPathFormatting( [NotNull] string path )
        {
            // Replace backslashes with forward slashes
            string formattedPath = path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar )
                .Replace( oldChar: '\\', Path.DirectorySeparatorChar )
                .Replace( oldChar: '/', Path.DirectorySeparatorChar );

            // Fix repeated slashes
            formattedPath = Regex.Replace(
                formattedPath,
                $"(?<!:){Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}+",
                Path.DirectorySeparatorChar.ToString()
            );

            // Fix trailing slashes
            formattedPath = formattedPath.TrimEnd( Path.DirectorySeparatorChar );

            return formattedPath;
        }
    }
}
