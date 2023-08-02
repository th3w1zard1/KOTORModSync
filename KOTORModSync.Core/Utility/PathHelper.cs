// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class PathValidator
    {
        // Characters not allowed in Windows file and directory names
        // we don't check colon or any slashes because we aren't validating file/folder names, only a full path string.
        [NotNull]
        private static readonly char[] s_invalidPathCharsWindows = {
            '<', '>', '"', '|', '?', '*',
            '\0', '\n', '\r', '\t', '\b', '\a', '\v', '\f',
        };

        // Characters not allowed in Unix file and directory names
        [NotNull]
        private static readonly char[] s_invalidPathCharsUnix = {
            '\0',
        };

        // Reserved file names in Windows
        [NotNull][ItemNotNull]
        private static readonly string[] s_reservedFileNamesWindows = {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        public static bool IsValidPath( [NotNull] string path)
        {
            if ( string.IsNullOrEmpty( path ) )
                return false;

            try
            {
                // Check for forbidden printable ASCII characters
                char[] invalidChars = GetInvalidCharsForPlatform();
                if (path.IndexOfAny( invalidChars ) >= 0)
                    return false;

                // Check for non-printable characters
                if ( ContainsNonPrintableChars(path) )
                    return false;

                // Check for reserved file names in Windows
                //if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                //{
                if ( IsReservedFileNameWindows(path) )
                    return false;
            
                // Check for invalid filename parts
                // ReSharper disable once ConvertIfStatementToReturnStatement
                if ( HasInvalidWindowsFileNameParts(path) )
                    return false;
                //}

                return true;
            }
            catch ( Exception e )
            {
                Logger.LogVerbose( e.Message );
                return false;
            }
        }

        private static char[] GetInvalidCharsForPlatform()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix
                ? s_invalidPathCharsUnix
                : s_invalidPathCharsWindows;
        }

        private static bool ContainsNonPrintableChars([CanBeNull] string path) => path?.Any( c => c < ' ' && c != '\t' ) ?? false;
        private static bool IsReservedFileNameWindows([NotNull] string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            
            // Check if any reserved filename matches the filename (case-insensitive)
            return s_reservedFileNamesWindows.Any(reservedName => string.Equals(reservedName, fileName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasInvalidWindowsFileNameParts(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);

            // Check for a filename ending with a period or space
            if (fileName.EndsWith(" ") || fileName.EndsWith("."))
                return true;

            // Check for consecutive periods in the filename
            for (int i = 0; i < fileName.Length - 1; i++)
            {
                if (fileName[i] == '.' && fileName[i + 1] == '.')
                    return true;
            }

            return false;
        }
    }

    public static class PathHelper
    {
        [CanBeNull]
        // if it's a folder, return path as is, if it's a file get the parent dir.
        public static string GetFolderName( [CanBeNull] string filePath )
        {
            return Path.HasExtension( filePath )
                ? Path.GetDirectoryName( filePath )
                : filePath;
        }

        public static DirectoryInfo TryGetValidDirectoryInfo(string destinationPath)
        {
            if ( destinationPath.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
                return null;

            try
            {
                return new DirectoryInfo(destinationPath);
            }
            catch (Exception)
            {
                // In .NET Framework 4.6.2, the DirectoryInfo constructor throws an exception
                // when the path is invalid. We catch the exception and return null instead.
                return null;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetLongPathName(string shortPath, StringBuilder longPath, int bufferSize);

        public static string ConvertWindowsPathToCaseSensitive( string path )
        {
            if ( Environment.OSVersion.Platform != PlatformID.Win32NT )
                return path;

            const int bufferSize = 260; // MAX_PATH on Windows
            var longPathBuffer = new StringBuilder(bufferSize);

            int result = GetLongPathName(path, longPathBuffer, bufferSize);
            // ReSharper disable once InvertIf
            if ( result <= 0 || result >= bufferSize )
            {
                // Handle the error, e.g., the function call failed, or the buffer was too small
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception( error );
            }

            // The function succeeded, and the result contains the case-sensitive long path
            return longPathBuffer.ToString();
        }

        [CanBeNull]
        public static string GetCaseSensitivePath([NotNull] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            
            if (!PathValidator.IsValidPath(path))
                throw new ArgumentException($"{path} is not a valid path!");

            path = Path.GetFullPath(path);
            if (File.Exists(path) || Directory.Exists(path))
                return ConvertWindowsPathToCaseSensitive(path);

            string parentDirPath = Path.GetDirectoryName(path)
                ?? throw new NullReferenceException($"Path.GetDirectoryName(path) when path is '{path}'");

            DirectoryInfo parentDir = TryGetValidDirectoryInfo(parentDirPath)
                ?? throw new NullReferenceException( "TryGetValidDirectoryInfo(parentDirPath)" );
            return !parentDir.Exists && !( parentDir = TryGetValidDirectoryInfo( GetCaseSensitivePath(parentDirPath) )
                ?? throw new DirectoryNotFoundException($"Could not find case-sensitive directory for path string '{parentDirPath}'") ).Exists
                    ? throw new DirectoryNotFoundException($"Could not find case-sensitive directory for path string '{parentDirPath}'")
                    : GetCaseSensitiveChildPath(parentDir, path);
        }

        private static string GetCaseSensitiveChildPath(DirectoryInfo parentDir, string path) =>
        (
            from item in parentDir?.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly)
            where item.FullName.Equals( path, StringComparison.OrdinalIgnoreCase )
            select ConvertWindowsPathToCaseSensitive( item.FullName )
        ).FirstOrDefault();

        [CanBeNull]
        public static string GetCaseSensitivePath( [NotNull] FileInfo file ) => GetCaseSensitivePath( file?.FullName );

        [CanBeNull]
        public static string GetCaseSensitivePath( [NotNull] DirectoryInfo directory ) => GetCaseSensitivePath( directory?.FullName );

        public static async Task MoveFileAsync( [NotNull] string sourcePath, [NotNull] string destinationPath )
        {
            if ( sourcePath is null )
                throw new ArgumentNullException( nameof( sourcePath ) );
            if ( destinationPath is null )
                throw new ArgumentNullException( nameof( destinationPath ) );

            using ( var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true
                ) )
            {
                using ( var destinationStream = new FileStream(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
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

                        if ( Directory.Exists( formattedPath ) )
                        {
                            IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                                formattedPath,
                                searchPattern: "*",
                                topLevelOnly
                                    ? SearchOption.TopDirectoryOnly
                                    : SearchOption.AllDirectories
                            );

                            result.AddRange( matchingFiles );
                        }

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
                        continue;

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

        [NotNull]
        public static List<FileSystemInfo> FindCaseInsensitiveDuplicates([NotNull] DirectoryInfo directory)
        {
            if (directory is null)
                throw new ArgumentNullException(nameof(directory));

            var duplicates = new List<FileSystemInfo>();

            try
            {
                FindDuplicatesRecursively(directory, duplicates);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return duplicates;
            }

            return duplicates;
        }

        private static void FindDuplicatesRecursively([NotNull] DirectoryInfo directory, [NotNull] List<FileSystemInfo> duplicates)
        {
            if (duplicates is null)
                throw new ArgumentNullException(nameof(duplicates));

            // Check if the directory exists and if we have access to it.
            if (!directory.Exists)
                throw new DirectoryNotFoundException("Directory not found.");

            var fileList = new Dictionary<string, List<FileSystemInfo>>(StringComparer.OrdinalIgnoreCase);
            var folderList = new Dictionary<string, List<FileSystemInfo>>(StringComparer.OrdinalIgnoreCase);

            // Search for files and add them to the file dictionary.
            foreach (FileInfo file in directory.GetFiles())
            {
                if (file?.Exists != true)
                    continue;

                string fileNameWithExtension = file.Name;
                if (!fileList.TryGetValue(fileNameWithExtension, out List<FileSystemInfo> files))
                {
                    files = new List<FileSystemInfo>();
                    fileList.Add(fileNameWithExtension, files);
                }

                files.Add(file);
            }

            // Search for subdirectories and add them to the folder dictionary.
            foreach (DirectoryInfo subdirectory in directory.GetDirectories())
            {
                if (subdirectory?.Exists != true)
                    continue;

                if (!folderList.TryGetValue(subdirectory.Name, out List<FileSystemInfo> folders))
                {
                    folders = new List<FileSystemInfo>();
                    folderList.Add(subdirectory.Name, folders);
                }

                folders.Add(subdirectory);

                // Recursively search the sub-directory.
                FindDuplicatesRecursively(subdirectory, duplicates);

                // Check for file duplicates within the current sub-directory.
                foreach (KeyValuePair<string, List<FileSystemInfo>> fileListEntry in fileList)
                {
                    List<FileSystemInfo> files = fileListEntry.Value;
                    if (files.Count > 1)
                        duplicates.AddRange(files);
                }

                // Check for folder duplicates within the current sub-directory.
                foreach (KeyValuePair<string, List<FileSystemInfo>> folderListEntry in folderList)
                {
                    List<FileSystemInfo> foldersInCurrentDir = folderListEntry.Value;
                    if (foldersInCurrentDir.Count > 1)
                        duplicates.AddRange(foldersInCurrentDir);
                }

                // Clear the dictionaries for the next sub-directory.
                fileList.Clear();
                folderList.Clear();
            }
        }


        [NotNull]
        public static List<FileSystemInfo> FindCaseInsensitiveDuplicates( [NotNull] string path )
        {
            if ( !PathValidator.IsValidPath( path ) )
                throw new ArgumentException( nameof( path ) + " is not a valid path string" );

            var directory = new DirectoryInfo( path );
            return FindCaseInsensitiveDuplicates( directory );
        }

        public static (FileSystemInfo, List<string>) GetClosestMatchingEntry( [NotNull] string path )
        {
            if ( !PathValidator.IsValidPath( path ) )
                throw new ArgumentException( nameof( path ) + " is not a valid path string" );

            string directoryName = Path.GetDirectoryName( path );
            string searchPattern = Path.GetFileName( path );

            FileSystemInfo closestMatch = null;
            int maxMatchingCharacters = -1;
            var duplicatePaths = new List<string>();

            if ( directoryName is null )
                return ( null, duplicatePaths );

            var directory = new DirectoryInfo( directoryName );
            foreach ( FileSystemInfo entry in directory.EnumerateFileSystemInfos( searchPattern ) )
            {
                if ( string.IsNullOrWhiteSpace( entry?.FullName ) )
                    continue;

                int matchingCharacters = GetMatchingCharactersCount( entry.FullName, path );
                if ( matchingCharacters == path.Length )
                {
                    // Exact match found
                    closestMatch = entry;
                }
                else if ( matchingCharacters > maxMatchingCharacters )
                {
                    closestMatch = entry;
                    maxMatchingCharacters = matchingCharacters;
                    duplicatePaths.Clear();
                }
                else if ( matchingCharacters == maxMatchingCharacters )
                {
                    duplicatePaths.Add( entry.FullName );
                }
            }

            return ( closestMatch, duplicatePaths );
        }

        private static int GetMatchingCharactersCount( [NotNull] string str1, [NotNull] string str2 )
        {
            if ( string.IsNullOrEmpty( str1 ) )
                throw new ArgumentException( message: "Value cannot be null or empty.", nameof( str1 ) );
            if ( string.IsNullOrEmpty( str2 ) )
                throw new ArgumentException( message: "Value cannot be null or empty.", nameof( str2 ) );

            int matchingCount = 0;

            for (
                int i = 0;
                i < str1.Length && i < str2.Length;
                i++
            )
            {
                if ( str1[i] != str2[i] )
                    break;

                matchingCount++;
            }

            return matchingCount;
        }
    }
}
