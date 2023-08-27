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
            if ( string.IsNullOrWhiteSpace( folderPath ) )
                return null;

            string formattedPath = FixPathFormatting( folderPath );
            if ( !PathValidator.IsValidPath( formattedPath ) )
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
            if ( string.IsNullOrWhiteSpace( filePath ) )
                return null;

            string formattedPath = FixPathFormatting( filePath );
            if ( !PathValidator.IsValidPath( formattedPath ) )
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
                dwDesiredAccess: 0,
                FILE_SHARE_READ,
                lpSecurityAttributes: IntPtr.Zero,
                dwCreationDisposition: OPEN_EXISTING,
                dwFlagsAndAttributes: FILE_FLAG_BACKUP_SEMANTICS,
                hTemplateFile: IntPtr.Zero
            );

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

        public static string GetRelativePath(string relativeTo, string path) => GetRelativePath(relativeTo, path, StringComparison.OrdinalIgnoreCase);

        private static string GetRelativePath(string relativeTo, string path, StringComparison comparisonType)
        {
            if (string.IsNullOrEmpty(relativeTo))
                throw new ArgumentException("Path cannot be empty", nameof(relativeTo));
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            relativeTo = Path.GetFullPath(FixPathFormatting(relativeTo));
            path = Path.GetFullPath(FixPathFormatting(path));

            if (!AreRootsEqual(relativeTo, path, comparisonType))
                return path;

            int commonLength = GetCommonPathLength(
                relativeTo,
                path,
                ignoreCase: comparisonType == StringComparison.OrdinalIgnoreCase
            );

            if (commonLength == 0)
                return path;

            bool pathEndsInSeparator = path.EndsWith(Path.DirectorySeparatorChar.ToString());
            int pathLength = path.Length;
            if (pathEndsInSeparator)
                pathLength--;

            if (relativeTo.Length == pathLength && commonLength >= relativeTo.Length) return ".";

            var sb = new StringBuilder(Math.Max(relativeTo.Length, path.Length));

            if (commonLength < relativeTo.Length)
            {
                sb.Append("..");

                for (int i = commonLength + 1; i < relativeTo.Length; i++)
                {
                    if (relativeTo[i] == Path.DirectorySeparatorChar)
                    {
                        sb.Append(Path.DirectorySeparatorChar);
                        sb.Append("..");
                    }
                }
            }
            else if (path[commonLength] == Path.DirectorySeparatorChar)
            {
                commonLength++;
            }

            int differenceLength = pathLength - commonLength;
            if (pathEndsInSeparator)
                differenceLength++;

            if (differenceLength > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }

                sb.Append(path.Substring(commonLength, differenceLength));
            }

            return sb.ToString();
        }

        private static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
        {
            int firstRootLength = Path.GetPathRoot(first).Length;
            int secondRootLength = Path.GetPathRoot(second).Length;

            return firstRootLength == secondRootLength
                   && 0 == string.Compare(
                        strA: first,
                        indexA: 0,
                        strB: second,
                        indexB: 0,
                        firstRootLength,
                        comparisonType
                   );
        }

        private static int GetCommonPathLength(string first, string second, bool ignoreCase)
        {
            int commonChars = Math.Min(first.Length, second.Length);

            int commonLength = 0;
            for (int i = 0; i < commonChars; i++)
            {
                if ( first[i] != Path.DirectorySeparatorChar && second[i] != Path.DirectorySeparatorChar )
                    continue;

                if ( 0 != string.Compare(
                    strA: first,
                    indexA: 0,
                    strB: second,
                    indexB: 0,
                    length: i + 1,
                    comparisonType: ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal) )
                {
                    return commonLength;
                }

                commonLength = i + 1;
            }

            return commonLength;
        }

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
        private static extern uint GetFinalPathNameByHandle( IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags );

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Auto )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool CloseHandle( IntPtr hObject );

        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile );

        
        public static FileSystemInfo GetCaseSensitivePath(FileSystemInfo fileSystemInfoItem)
        {
            switch ( fileSystemInfoItem )
            {
                case DirectoryInfo dirInfo: return GetCaseSensitivePath( dirInfo );
                case FileInfo fileInfo: return GetCaseSensitivePath( fileInfo );
                default: return null;
            }
        }
        
        public static FileInfo GetCaseSensitivePath( FileInfo file )
        {
            ( string thisFilePath, _ ) = GetCaseSensitivePath( file?.FullName, isFile: true);
            return new FileInfo( thisFilePath );
        }
        
        public static DirectoryInfo GetCaseSensitivePath( DirectoryInfo file )
        {
            ( string thisFilePath, _ ) = GetCaseSensitivePath( file?.FullName, isFile: true);
            return new DirectoryInfo( thisFilePath );
        }

        public static (string, bool?) GetCaseSensitivePath(string path, bool? isFile = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));

            string formattedPath = Path.GetFullPath(FixPathFormatting(path));

            // quick lookup
            bool fileExists = File.Exists(formattedPath);
            bool folderExists = Directory.Exists(formattedPath);
            if (fileExists && (isFile == true || !folderExists)) return (ConvertWindowsPathToCaseSensitive(formattedPath), true);
            if (folderExists && (isFile == false || !fileExists)) return (ConvertWindowsPathToCaseSensitive(formattedPath), false);

            string[] parts = formattedPath.Split(new [] {Path.DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);

            // no path parts available (no separators found). Maybe it's a file/folder that exists in cur directory.
            if (parts.Length == 0)
                parts = new[] { formattedPath };

            // insert the root into the list (will be / on unix, and drive name (e.g. C:\\ on windows)
            string currentPath = Path.GetPathRoot(formattedPath);
            if (!string.IsNullOrEmpty(currentPath) && !Path.IsPathRooted(parts[0]))
                parts = new[] { currentPath }.Concat( parts ).ToArray();
            // append directory separator to drive roots
            if (parts[0].EndsWith(":"))
                parts[0] += Path.DirectorySeparatorChar;

            int largestExistingPathPartsIndex = -1;
            string caseSensitiveCurrentPath = null;
            for (int i = 1; i < parts.Length; i++)
            {
                // find the closest matching file/folder in the current path for unix, useful for duplicates.
                string previousCurrentPath = Path.Combine(parts.Take(i).ToArray());
                currentPath = Path.Combine(previousCurrentPath, parts[i]);
                if (Environment.OSVersion.Platform != PlatformID.Win32NT && Directory.Exists(previousCurrentPath))
                {
                    int maxMatchingCharacters = -1;
                    string closestMatch = parts[i];

                    foreach (
                        FileSystemInfo folderOrFileInfo
                        in new DirectoryInfo( previousCurrentPath )
                        .EnumerateFileSystemInfosSafely( searchPattern: "*", SearchOption.TopDirectoryOnly )
                    )
                    {
                        if (folderOrFileInfo is null || !folderOrFileInfo.Exists)
                            continue;

                        int matchingCharacters = GetMatchingCharactersCount(folderOrFileInfo.Name, parts[i]);
                        if ( matchingCharacters > maxMatchingCharacters )
                        {
                            maxMatchingCharacters = matchingCharacters;
                            closestMatch = folderOrFileInfo.Name;
                            if ( i == parts.Length )
                                isFile = folderOrFileInfo is FileInfo;
                        }
                    }

                    parts[i] = closestMatch;
                }
                // resolve case-sensitive pathing. largestExistingPathPartsIndex determines the largest index of the existing path parts.
                // todo: check if it's the last part of the path, then conditionally call directory.exists OR file.exists based on isFile.
                else if ( !File.Exists(currentPath)
                    && !Directory.Exists(currentPath)
                    && string.IsNullOrEmpty(caseSensitiveCurrentPath) )
                {
                    // Get the case-sensitive path based on the existing parts we've determined.
                    largestExistingPathPartsIndex = i;
                    caseSensitiveCurrentPath = ConvertWindowsPathToCaseSensitive(previousCurrentPath);
                }
            }

            if ( caseSensitiveCurrentPath is null )
                return ( Path.Combine( parts ), isFile );

            string combinedPath = largestExistingPathPartsIndex > -1
                ? Path.Combine(
                    caseSensitiveCurrentPath,
                    Path.Combine( parts.Skip( largestExistingPathPartsIndex ).ToArray() )
                )
                : Path.Combine( parts );

            return ( combinedPath, isFile );
        }

        private static int GetMatchingCharactersCount(string str1, string str2)
        {
            if (string.IsNullOrEmpty(str1))
                throw new ArgumentException("Value cannot be null or empty.", nameof(str1));
            if (string.IsNullOrEmpty(str2))
                throw new ArgumentException("Value cannot be null or empty.", nameof(str2));

            int matchingCount = 0;
            for (int i = 0; i < str1.Length && i < str2.Length; i++)
            {
                // don't consider a match if any char in the paths are not case-insensitive matches.
                if (char.ToLowerInvariant(str1[i]) != char.ToLowerInvariant(str2[i]))
                    return -1;

                // increment matching count if case-sensitive match at this char index succeeds
                if (str1[i] == str2[i])
                    matchingCount++;
            }

            return matchingCount;
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
                    string formattedPath = FixPathFormatting( path );

                    // ReSharper disable once AssignNullToNotNullAttribute
                    if ( !ContainsWildcards( formattedPath ) )
                    {
                        // Handle non-wildcard paths
                        if ( File.Exists( formattedPath ) )
                            result.Add( formattedPath );

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

                    var currentDirInfo = new DirectoryInfo( currentDir );
					
                    IEnumerable<FileInfo> checkFiles = currentDirInfo.EnumerateFilesSafely(
	                    searchPattern: "*",
	                    includeSubFolders
		                    ? SearchOption.AllDirectories
		                    : SearchOption.TopDirectoryOnly
                    );

                    result.AddRange(
	                    from thisFile in checkFiles
	                    where thisFile != null
		                    && WildcardPathMatch( thisFile.FullName, formattedPath )
	                    select thisFile.FullName
                    );

                    if ( MainConfig.CaseInsensitivePathing )
                    {
	                    IEnumerable<FileSystemInfo> duplicates = FindCaseInsensitiveDuplicates(
		                    currentDir,
		                    includeSubFolders: true,
		                    isFile: false
	                    );

	                    foreach ( FileSystemInfo thisDuplicateFolder in duplicates )
	                    {
		                    // Get all files in the parent directory.
		                    if ( !(thisDuplicateFolder is DirectoryInfo dirInfo) )
			                    throw new NullReferenceException(nameof( dirInfo ));

		                    checkFiles = dirInfo.EnumerateFilesSafely(
			                    searchPattern: "*",
			                    includeSubFolders
				                    ? SearchOption.AllDirectories
				                    : SearchOption.TopDirectoryOnly
		                    );

		                    result.AddRange(
			                    from thisFile in checkFiles
			                    where thisFile != null
				                    && WildcardPathMatch( thisFile.FullName, formattedPath )
			                    select thisFile.FullName
		                    );
	                    }
                    }
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
            if ( formattedPath.Length > 1 )
                formattedPath = formattedPath.TrimEnd( Path.DirectorySeparatorChar );

            return formattedPath;
        }

        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates( DirectoryInfo dirInfo, bool includeSubFolders = true )
        {
            // ReSharper disable once AssignNullToNotNullAttribute - no point duplicating the null check
            return FindCaseInsensitiveDuplicates( dirInfo?.FullName, includeSubFolders, isFile: false );
        }

        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates( FileInfo fileInfo )
        {
            // ReSharper disable once AssignNullToNotNullAttribute - no point duplicating the null check
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

            if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
	            yield break;

            // determine if path is a folder or a file, and resolve case-insensitive pathing.
            DirectoryInfo dirInfo = null;
            string fileName = Path.GetFileName( formattedPath );
            switch ( isFile )
            {
	            case false:
		            {
			            dirInfo = new DirectoryInfo( formattedPath );
			            if ( !dirInfo.Exists )
			            {
				            dirInfo = new DirectoryInfo(
					            GetCaseSensitivePath( formattedPath ).Item1
				            );
			            }

			            break;
		            }
	            case true:
		            {
			            string parentDir = Path.GetDirectoryName( formattedPath );
			            if ( !string.IsNullOrEmpty(parentDir) && !( dirInfo = new DirectoryInfo( parentDir ) ).Exists )
			            {
				            dirInfo = new DirectoryInfo(
					            GetCaseSensitivePath( parentDir ).Item1
				            );
			            }

			            break;
		            }
	            default:
		            {
			            dirInfo = new DirectoryInfo( formattedPath );
			            string caseSensitivePath = formattedPath;
			            if ( !dirInfo.Exists )
			            {
				            caseSensitivePath = GetCaseSensitivePath( formattedPath ).Item1;
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

            if ( !dirInfo?.Exists ?? false )
                throw new ArgumentException( $"Path item doesn't exist on disk: '{formattedPath}'" );

            // build duplicate files/folders list
            var fileList = new Dictionary<string, List<FileSystemInfo>>( StringComparer.OrdinalIgnoreCase );
            var folderList = new Dictionary<string, List<FileSystemInfo>>( StringComparer.OrdinalIgnoreCase );
            foreach ( FileInfo file in dirInfo.GetFilesSafely() )
            {
                if ( !file.Exists )
                    continue;
                if (isFile == true && !file.Name.Equals( fileName, StringComparison.OrdinalIgnoreCase ))
                    continue;

                string filePath = file.FullName.ToLowerInvariant();
                if ( !fileList.TryGetValue( filePath, out List<FileSystemInfo> files ) )
                {
                    files = new List<FileSystemInfo>();
                    fileList.Add( filePath, files );
                }
                files.Add( file );
            }

            foreach ( List<FileSystemInfo> files in fileList.Values )
            {
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

            foreach ( DirectoryInfo subDirectory in dirInfo.EnumerateDirectoriesSafely() )
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

            foreach ( List<FileSystemInfo> foldersInCurrentDir in folderList.Values )
            {
                if ( foldersInCurrentDir.Count <= 1 )
                    continue;

                foreach ( FileSystemInfo duplicate in foldersInCurrentDir )
                {
                    yield return duplicate;
                }
            }
        }
    }
}
