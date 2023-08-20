// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
{
    public static class PathHelper
    {
        // if it's a folder, return path as is, if it's a file get the parent dir.
        [CanBeNull]
        public static string GetFolderName( [CanBeNull] string filePath )
        {
            return Path.HasExtension( filePath )
                ? Path.GetDirectoryName( filePath )
                : filePath;
        }

        [CanBeNull]
        public static DirectoryInfo TryGetValidDirectoryInfo( [CanBeNull] string folderPath )
        {
            string formattedPath = FixPathFormatting( folderPath );
            if ( formattedPath is null || PathValidator.IsValidPath( formattedPath ) )
                return null;

            try
            {
                return new DirectoryInfo( formattedPath );
            }
            catch ( Exception )
            {
                // In .NET Framework 4.6.2 and earlier, the DirectoryInfo constructor throws an exception
                // when the path is invalid. We catch the exception and return null instead for a unified experience.
                return null;
            }
        }

        [CanBeNull]
        public static FileInfo TryGetValidFileInfo( [CanBeNull] string filePath )
        {
            string formattedPath = FixPathFormatting( filePath );
            if ( formattedPath is null || PathValidator.IsValidPath( formattedPath ) )
                return null;

            try
            {
                return new FileInfo( formattedPath );
            }
            catch ( Exception )
            {
                // In .NET Framework 4.6.2 and earlier, the FileInfo constructor throws an exception
                // when the path is invalid. We catch the exception and return null instead for a unified experience.
                return null;
            }
        }

        [DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
        private static extern int GetLongPathName( string shortPath, StringBuilder longPath, int bufferSize );

        public static string ConvertWindowsPathToCaseSensitive( string path )
        {
            if ( Environment.OSVersion.Platform != PlatformID.Win32NT )
                return path;
            if ( string.IsNullOrWhiteSpace( path ) )
                throw new ArgumentException( $"'{nameof( path )}' cannot be null or whitespace.", nameof( path ) );
            if ( !PathValidator.IsValidPath( path ) )
                throw new ArgumentException( $"{path} is not a valid path!" );


            const uint FILE_SHARE_READ = 1;
            const uint OPEN_EXISTING = 3;
            const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            const uint VOLUME_NAME_DOS = 0;

            IntPtr handle = CreateFile(
                path,
                0,
                FILE_SHARE_READ,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero );

            if ( handle == IntPtr.Zero )
                throw new Win32Exception( Marshal.GetLastWin32Error() );

            try
            {
                var buffer = new StringBuilder( 4096 );
                uint result = GetFinalPathNameByHandle( handle, buffer, (uint)buffer.Capacity, VOLUME_NAME_DOS );

                if ( result == 0 )
                    throw new Win32Exception( Marshal.GetLastWin32Error() );

                // The result may be prefixed with "\\?\"
                string finalPath = buffer.ToString();
                const string prefix = @"\\?\";
                if ( finalPath.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
                    finalPath = finalPath.Substring( prefix.Length );

                return finalPath;
            }
            finally
            {
                _ = CloseHandle( handle );
            }
        }

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Auto )]
        private static extern uint GetFinalPathNameByHandle( IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags );

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Auto )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool CloseHandle( IntPtr hObject );

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Auto )]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile );

        [CanBeNull] public static string GetCaseSensitivePath( FileInfo file ) => GetCaseSensitivePath( file?.FullName );
        [CanBeNull] public static string GetCaseSensitivePath( DirectoryInfo directory ) => GetCaseSensitivePath( directory?.FullName );

        public static string GetCaseSensitivePath( string path )
        {
            if ( string.IsNullOrWhiteSpace( path ) )
                throw new ArgumentException( $"'{nameof( path )}' cannot be null or whitespace.", nameof( path ) );

            string formattedPath = FixPathFormatting( Path.GetFullPath( path ) );
            if ( File.Exists( formattedPath ) || Directory.Exists( formattedPath ) )
                return ConvertWindowsPathToCaseSensitive( formattedPath );

            var parts = formattedPath.Split(
                new[] { Path.DirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            ).ToList();

            if ( parts.Count == 0 )
                parts.Insert( index: 0, formattedPath );

            string currentPath = Path.GetPathRoot( formattedPath );
            if ( currentPath != parts[0] && !string.IsNullOrEmpty( currentPath ) )
                parts.Insert( index: 0, currentPath );

            int largestExistingPathPartsIndex = -1;
            string caseSensitiveCurrentPath = null;
            for ( int i = 1; i < parts.Count; i++ )
            {
                var parentDir = new DirectoryInfo( Path.Combine( parts.Take( i ).ToArray() ) );
                if ( parentDir.Exists )
                {
                    if ( GetCaseSensitiveChildItem( parentDir, parts[i] ) is string childItem )
                        parts[i] = childItem;
                }
                // if the root path cannot be determined, return original path as is.
                else if ( i == 1 )
                {
                    return path;
                }
                currentPath = Path.Combine( currentPath, parts[i] );
                if ( !File.Exists( currentPath )
                    && !Directory.Exists( currentPath )
                    && string.IsNullOrEmpty( caseSensitiveCurrentPath ) )
                {
                    // Get the case-sensitive path based on the existing parts we've determined.
                    largestExistingPathPartsIndex = i;
                    caseSensitiveCurrentPath = ConvertWindowsPathToCaseSensitive( parentDir.FullName );
                }
            }

            return largestExistingPathPartsIndex > -1
                ? Path.Combine(
                    caseSensitiveCurrentPath,
                    Path.Combine( parts.Skip( largestExistingPathPartsIndex ).ToArray() )
                )
                : Path.Combine( parts.ToArray() );
        }


        [CanBeNull]
        private static string GetCaseSensitiveChildItem(DirectoryInfo parentDir, string finalPathPart)
        {
            return parentDir?.GetFileSystemInfos("*")
                .FirstOrDefault(item => item.Name.Equals(finalPathPart, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }
        

        public static async Task MoveFileAsync( string sourcePath, string destinationPath )
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

        public static List<string> EnumerateFilesWithWildcards(
            IEnumerable<string> filesAndFolders,
            bool includeSubFolders = true
        )
        {
            if ( filesAndFolders is null )
                throw new ArgumentNullException( nameof( filesAndFolders ) );

            var result = new List<string>();
            var uniquePaths = new HashSet<string>( filesAndFolders );

            foreach ( string path in uniquePaths )
            {
                if ( string.IsNullOrEmpty( path ) )
                    continue;

                try
                {
                    var formattedPath = new InsensitivePath( path );

                    // ReSharper disable once AssignNullToNotNullAttribute
                    if ( !ContainsWildcards( formattedPath ) )
                    {
                        // Handle non-wildcard paths
                        if ( File.Exists( formattedPath ) )
                            result.Add( formattedPath );
                        else if ( Directory.Exists( formattedPath ) )
                        {
                            IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                                formattedPath,
                                searchPattern: "*",
                                includeSubFolders
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly
                            );

                            result.AddRange( matchingFiles );
                        }

                        continue;
                    }

                    // Handle wildcard paths
                    //
                    // determine the closest parent folder in hierarchy that doesn't have wildcards
                    // then wildcard match them all by hierarchy level.
                    string currentDir = formattedPath;
                    while ( ContainsWildcards( currentDir ) )
                    {
                        string parentDirectory = Path.GetDirectoryName( currentDir );

                        // Exit the loop if no parent directory is found or if the parent directory is the same as the current directory
                        if ( string.IsNullOrEmpty( parentDirectory ) || parentDirectory == currentDir )
                            break;

                        currentDir = parentDirectory;
                    }

                    if ( !Directory.Exists( currentDir ) )
                        continue;

                    // Get all files in the parent directory.
                    IEnumerable<string> checkFiles = Directory.EnumerateFiles(
                        currentDir,
                        searchPattern: "*",
                        includeSubFolders
                            ? SearchOption.AllDirectories
                            : SearchOption.TopDirectoryOnly
                    );

                    // wildcard match them all with WildcardPatchMatch and add to result
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


        private static bool ContainsWildcards( [NotNull] string path ) => path.Contains( '*' ) || path.Contains( '?' );


        public static bool WildcardPathMatch( string input, string patternInput )
        {
            if ( input is null )
                throw new ArgumentNullException( nameof( input ) );
            if ( patternInput is null )
                throw new ArgumentNullException( nameof( patternInput ) );

            // Fix path formatting
            input = FixPathFormatting( input );
            patternInput = FixPathFormatting( patternInput );

            // Split the input and patternInput into directory levels
            string[] inputLevels = input?.Split( Path.DirectorySeparatorChar );
            string[] patternLevels = patternInput?.Split( Path.DirectorySeparatorChar );

            // Ensure the number of levels match
            if ( inputLevels?.Length != patternLevels?.Length )
                return false;

            // Iterate over each level and perform wildcard matching
            for ( int i = 0; i < inputLevels?.Length; i++ )
            {
                string inputLevel = inputLevels[i];
                string patternLevel = patternLevels[i];

                if ( patternLevel is "*" )
                    continue;

                // Check if the current level matches the pattern
                if ( !WildcardMatch( inputLevel, patternLevel ) )
                    return false;
            }

            return true;
        }

        // Most end users don't know Regex, this function will convert basic wildcards to regex patterns.
        private static bool WildcardMatch( string input, string patternInput )
        {
            if ( input is null )
                throw new ArgumentNullException( nameof( input ) );
            if ( patternInput is null )
                throw new ArgumentNullException( nameof( patternInput ) );

            // Escape special characters in the pattern
            patternInput = Regex.Escape( patternInput );

            // Replace * with .* and ? with . in the pattern
            patternInput = patternInput
                .Replace( oldValue: @"\*", newValue: ".*" )
                .Replace( oldValue: @"\?", newValue: "." );

            // Use regex to perform the wildcard matching
            return Regex.IsMatch( input, $"^{patternInput}$" );
        }


        [NotNull]
        public static string FixPathFormatting( [NotNull] string path )
        {
            if ( path is null )
                throw new ArgumentNullException( nameof( path ) );

            if ( string.IsNullOrWhiteSpace( path ) )
                return path;

            // Replace all slashes with the operating system's path separator
            string formattedPath = path
                .Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar )
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

        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates( DirectoryInfo dirInfo, bool includeSubFolders = true )
        {
            return FindCaseInsensitiveDuplicates( dirInfo?.FullName, includeSubFolders, isFile: false );
        }

        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates( FileInfo fileInfo )
        {
            return FindCaseInsensitiveDuplicates( fileInfo?.FullName, isFile: true );
        }

        // Finds all duplicate items in a path.
        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates( [NotNull] string path, bool includeSubFolders = true, bool? isFile = null )
        {
            if ( path is null )
                throw new ArgumentNullException( nameof( path ) );

            string formattedPath = FixPathFormatting( path );
            if ( !PathValidator.IsValidPath( formattedPath ) )
                throw new ArgumentException( $"'{path}' is not a valid path string" );

            // determine if path is a folder or a file.
            DirectoryInfo dirInfo;
            string fileName = Path.GetFileName( formattedPath );
            switch ( isFile )
            {
                case false:
                    {
                        dirInfo = new DirectoryInfo( formattedPath );
                        if ( !dirInfo.Exists )
                            dirInfo = new DirectoryInfo( GetCaseSensitivePath( formattedPath ) );
                        break;
                    }
                case true:
                    {
                        string parentDir = Path.GetDirectoryName( formattedPath );
                        dirInfo = new DirectoryInfo( parentDir );
                        if ( !dirInfo.Exists )
                            dirInfo = new DirectoryInfo( GetCaseSensitivePath( parentDir ) );
                        break;
                    }
                default:
                    {
                        dirInfo = new DirectoryInfo( formattedPath );
                        string caseSensitivePath = formattedPath;
                        if ( !dirInfo.Exists )
                        {
                            caseSensitivePath = GetCaseSensitivePath( formattedPath );
                            dirInfo = new DirectoryInfo( caseSensitivePath );
                        }

                        if ( !dirInfo.Exists )
                        {
                            string folderPath = Path.GetDirectoryName( caseSensitivePath );
                            isFile = true;
                            if ( !( folderPath is null ) )
                                dirInfo = new DirectoryInfo( folderPath );
                        }

                        break;
                    }
            }

            if ( !dirInfo.Exists )
                throw new ArgumentException( $"Path item doesn't exist on disk: '{formattedPath}'" );

            // build duplicate files/folders list
            var fileList = new Dictionary<string, List<FileSystemInfo>>( StringComparer.OrdinalIgnoreCase );
            var folderList = new Dictionary<string, List<FileSystemInfo>>( StringComparer.OrdinalIgnoreCase );
            foreach ( FileInfo file in dirInfo.GetFiles() )
            {
                if ( !file.Exists )
                    continue;
                if ( isFile == true && !file.Name.Equals( fileName, StringComparison.OrdinalIgnoreCase ) )
                    continue;

                string filePath = file.FullName.ToLowerInvariant();
                if ( !fileList.TryGetValue( filePath, out List<FileSystemInfo> files ) )
                {
                    files = new List<FileSystemInfo>();
                    fileList.Add( filePath, files );
                }
                files.Add( file );
            }

            foreach ( KeyValuePair<string, List<FileSystemInfo>> fileListEntry in fileList )
            {
                List<FileSystemInfo> files = fileListEntry.Value;
                if ( files.Count <= 1 )
                    continue;

                foreach ( FileSystemInfo duplicate in files )
                {
                    yield return duplicate;
                }
            }

            // don't iterate folders in the parent folder if original path is a file.
            if ( isFile == true )
                yield break;

            foreach ( DirectoryInfo subDirectory in dirInfo.GetDirectories() )
            {
                if ( !subDirectory.Exists )
                    continue;

                if ( !folderList.TryGetValue(
                    subDirectory.FullName.ToLowerInvariant(),
                    out List<FileSystemInfo> folders
                ) )
                {
                    folders = new List<FileSystemInfo>();
                    folderList.Add( subDirectory.FullName.ToLowerInvariant(), folders );
                }

                folders.Add( subDirectory );

                if ( includeSubFolders )
                {
                    foreach ( FileSystemInfo duplicate in FindCaseInsensitiveDuplicates( subDirectory ) )
                    {
                        yield return duplicate;
                    }
                }
            }

            foreach ( KeyValuePair<string, List<FileSystemInfo>> folderListEntry in folderList )
            {
                List<FileSystemInfo> foldersInCurrentDir = folderListEntry.Value;
                if ( foldersInCurrentDir.Count <= 1 )
                    continue;

                foreach ( FileSystemInfo duplicate in foldersInCurrentDir )
                {
                    yield return duplicate;
                }
            }
        }

        public static (FileSystemInfo, List<FileSystemInfo>) GetClosestMatchingEntry( string path )
        {
            if ( !PathValidator.IsValidPath( path ) )
                throw new ArgumentException( nameof( path ) + " is not a valid path string" );

            FileSystemInfo closestMatch = null;
            int maxMatchingCharacters = -1;
            string formattedPath = FixPathFormatting( path );
            var duplicatePaths = FindCaseInsensitiveDuplicates( formattedPath ).ToList();

            foreach ( FileSystemInfo duplicate in duplicatePaths )
            {
                int matchingCharacters = GetMatchingCharactersCount( duplicate?.FullName, path );
                if ( matchingCharacters > maxMatchingCharacters )
                {
                    closestMatch = duplicate;
                    maxMatchingCharacters = matchingCharacters;
                }
            }

            if ( !( closestMatch is null ) )
                return (closestMatch, duplicatePaths);

            if ( File.Exists( formattedPath ) )
                return (new FileInfo( formattedPath ), duplicatePaths);
            if ( Directory.Exists( formattedPath ) )
                return (new DirectoryInfo( formattedPath ), duplicatePaths);

            string caseSensitivePath = GetCaseSensitivePath( path );
            if ( File.Exists( caseSensitivePath ) )
                return (new FileInfo( caseSensitivePath ), duplicatePaths);
            if ( Directory.Exists( caseSensitivePath ) )
                return (new DirectoryInfo( caseSensitivePath ), duplicatePaths);

            return (null, new List<FileSystemInfo>());
        }

        private static int GetMatchingCharactersCount( string str1, string str2 )
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
                // don't consider a match if any char in the paths are not case-insensitive matches.
                if ( char.ToLowerInvariant( str1[i] ) != char.ToLowerInvariant( str2[i] ) )
                    return 0;

                // increment matching count if case-sensitive match at this char index succeeds
                if ( str1[i] == str2[i] )
                    matchingCount++;
            }

            return matchingCount;
        }
    }
}
