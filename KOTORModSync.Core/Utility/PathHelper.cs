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

namespace KOTORModSync.Core.Utility
{
    public static class PathValidator
    {
        // Characters not allowed in Windows file and directory names
        // we don't check colon or any slashes because we aren't validating file/folder names, only a full path string.
        private static readonly char[] s_invalidPathCharsWindows = {
            '<', '>', '"', '|', '?', '*',
            '\0', '\n', '\r', '\t', '\b', '\a', '\v', '\f',
        };

        // Characters not allowed in Unix file and directory names
        private static readonly char[] s_invalidPathCharsUnix = {
            '\0',
        };

        // Reserved file names in Windows
        private static readonly string[] s_reservedFileNamesWindows = {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        // Checks if the path is valid on running platform, or optionally (default) enforce for all platforms.
        public static bool IsValidPath( [CanBeNull] string path, bool enforceAllPlatforms=true)
        {
            if ( string.IsNullOrWhiteSpace( path ) )
                return false;
            if ( path == string.Empty )
                return false;

            try
            {
                // Check for forbidden printable ASCII characters
                char[] invalidChars = enforceAllPlatforms
                    ? s_invalidPathCharsWindows // already contains the unix ones
                    : GetInvalidCharsForPlatform();

                if ( path.IndexOfAny( invalidChars ) >= 0 )
                    return false;

                // Check for non-printable characters
                if ( ContainsNonPrintableChars(path) )
                    return false;

                // Check for reserved file names in Windows
                if ( enforceAllPlatforms || RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                {
                    if ( IsReservedFileNameWindows(path) )
                        return false;
                
                    // Check for invalid filename parts
                    // ReSharper disable once ConvertIfStatementToReturnStatement
                    if ( HasInvalidWindowsFileNameParts(path) )
                        return false;
                }

                // double-check
                try
                {
                    var _ = new FileInfo(path);
                    var __ = new DirectoryInfo(path);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return false;
                }
            }
            catch ( Exception e )
            {
                Console.WriteLine( e );
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
        private static bool IsReservedFileNameWindows(string path)
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
        // if it's a folder, return path as is, if it's a file get the parent dir.
        [CanBeNull]
        public static string GetFolderName( [CanBeNull] string filePath )
        {
            return Path.HasExtension( filePath )
                ? Path.GetDirectoryName( filePath )
                : filePath;
        }

        [CanBeNull]
        public static DirectoryInfo TryGetValidDirectoryInfo([CanBeNull] string folderPath)
        {
            string formattedPath = FixPathFormatting(folderPath);
            if ( formattedPath is null || PathValidator.IsValidPath(formattedPath) )
                return null;

            try
            {
                return new DirectoryInfo(formattedPath);
            }
            catch (Exception)
            {
                // In .NET Framework 4.6.2 and earlier, the DirectoryInfo constructor throws an exception
                // when the path is invalid. We catch the exception and return null instead for a unified experience.
                return null;
            }
        }

        [CanBeNull]
        public static FileInfo TryGetValidFileInfo([CanBeNull] string filePath)
        {
            string formattedPath = FixPathFormatting(filePath);
            if ( formattedPath is null || PathValidator.IsValidPath(formattedPath) )
                return null;

            try
            {
                return new FileInfo(formattedPath);
            }
            catch (Exception)
            {
                // In .NET Framework 4.6.2 and earlier, the FileInfo constructor throws an exception
                // when the path is invalid. We catch the exception and return null instead for a unified experience.
                return null;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetLongPathName(string shortPath, StringBuilder longPath, int bufferSize);

        public static string ConvertWindowsPathToCaseSensitive(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            if (!PathValidator.IsValidPath(path))
                throw new ArgumentException($"{path} is not a valid path!");

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return path;

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
                IntPtr.Zero);

            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var buffer = new StringBuilder(4096);
                uint result = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, VOLUME_NAME_DOS);

                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // The result may be prefixed with "\\?\"
                string finalPath = buffer.ToString();
                const string prefix = @"\\?\";
                if (finalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    finalPath = finalPath.Substring(prefix.Length);
                }

                return finalPath;
            }
            finally
            {
                _ = CloseHandle( handle );
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetFinalPathNameByHandle(IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
        
        [CanBeNull] public static string GetCaseSensitivePath( FileInfo file ) => GetCaseSensitivePath( file?.FullName );
        [CanBeNull] public static string GetCaseSensitivePath( DirectoryInfo directory ) => GetCaseSensitivePath( directory?.FullName );

        public static string GetCaseSensitivePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            if (!PathValidator.IsValidPath(path))
                throw new ArgumentException($"{path} is not a valid path!");

            path = Path.GetFullPath(path);
            if (File.Exists(path) || Directory.Exists(path))
                return ConvertWindowsPathToCaseSensitive(path);

            string originalPath = path;
            var caseSensitivePath = new StringBuilder();
            string[] parts = path.Split(Path.DirectorySeparatorChar);

            // Start with the root directory
            string currentPath = parts[0] + Path.DirectorySeparatorChar;
            _ = caseSensitivePath.Append( currentPath );

            for (int i = 1; i < parts.Length; i++)
            {
                currentPath = Path.Combine(currentPath, parts[i]);

                if (Directory.Exists(currentPath))
                {
                    var parentDir = new DirectoryInfo(Path.GetDirectoryName(currentPath));
                    DirectoryInfo[] childDirs = parentDir.GetDirectories(parts[i], SearchOption.TopDirectoryOnly);

                    if (childDirs.Length == 0)
                    {
                        // If there are no directories matching the name, then it's likely a non-existent subdirectory
                        // Append it as-is
                        _ = caseSensitivePath.Append( parts[i] + Path.DirectorySeparatorChar );
                    }
                    else
                    {
                        // Append the correct case version of the directory name
                        _ = caseSensitivePath.Append( childDirs[0].Name + Path.DirectorySeparatorChar );
                    }
                }
                else
                {
                    // Directory doesn't exist yet, append it as-is
                    // Get the case-sensitive path based on the existing parts we've determined.
                    string currentExistingPath = caseSensitivePath.ToString();
                    if (File.Exists( currentExistingPath ) || Directory.Exists( currentExistingPath ))
                    {
                        string caseSensitiveCurrentPath = ConvertWindowsPathToCaseSensitive(caseSensitivePath.ToString());
                        caseSensitivePath.Clear();
                        caseSensitivePath.Append(caseSensitiveCurrentPath + Path.DirectorySeparatorChar);
                    }

                    _ = caseSensitivePath.Append( parts[i] + Path.DirectorySeparatorChar );
                }
            }

            return caseSensitivePath.ToString().TrimEnd( Path.DirectorySeparatorChar );
        }



        [CanBeNull]
        private static string GetCaseSensitiveChildPath([CanBeNull] DirectoryInfo parentDir, string path) =>
        (
            from item in parentDir?.GetFileSystemInfos(searchPattern: "*", SearchOption.TopDirectoryOnly)
            where item.FullName.Equals( path, StringComparison.OrdinalIgnoreCase )
            select ConvertWindowsPathToCaseSensitive( item.FullName )
        ).FirstOrDefault();


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

            foreach (string path in uniquePaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                try
                {
                    string formattedPath = FixPathFormatting(path);
                    if (!PathValidator.IsValidPath(formattedPath))
                        throw new ArgumentException($"Not a valid path: '{path}'");
                    
                    // ReSharper disable once AssignNullToNotNullAttribute
                    if (!ContainsWildcards(formattedPath))
                    {
                        // Handle non-wildcard paths
                        if (File.Exists(formattedPath))
                        {
                            result.Add(formattedPath);
                        }
                        else if (Directory.Exists(formattedPath))
                        {
                            IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                                formattedPath,
                                searchPattern: "*",
                                includeSubFolders
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly
                            );

                            result.AddRange(matchingFiles);
                        }

                        continue;
                    }

                    // Handle simple wildcard paths
                    if (PathValidator.IsValidPath(formattedPath))
                    {
                        string parentDir = Path.GetDirectoryName(formattedPath);
                        if ( !(parentDir is null) && Directory.Exists(parentDir) )
                        {
                            IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                                parentDir,
                                Path.GetFileName(formattedPath),
                                includeSubFolders
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly
                            );

                            result.AddRange(matchingFiles);
                            continue;
                        }
                    }

                    // Handle wildcard paths
                    //
                    // determine the closest parent folder in hierarchy that doesn't have wildcards
                    // then wildcard match them all by hierarchy level.
                    string currentDir = formattedPath;
                    while (ContainsWildcards(currentDir))
                    {
                        string parentDirectory = Path.GetDirectoryName(currentDir);

                        // Exit the loop if no parent directory is found or if the parent directory is the same as the current directory
                        if (string.IsNullOrEmpty(parentDirectory) || parentDirectory == currentDir)
                            break;

                        currentDir = parentDirectory;
                    }

                    if (!Directory.Exists(currentDir))
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
                    result.AddRange(checkFiles.Where(thisFile => WildcardPathMatch(thisFile, formattedPath)));
                }
                catch (Exception ex)
                {
                    // Handle or log the exception as required
                    Console.WriteLine($"An error occurred while processing path '{path}': {ex.Message}");
                }
            }

            return result;
        }


        public static bool ContainsWildcards( [NotNull] string path ) => path.Contains( '*' ) || path.Contains( '?' );


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

                if (patternLevel is "*")
                    continue;

                // Check if the current level matches the pattern
                if ( !WildcardMatch( inputLevel, patternLevel ) )
                    return false;
            }

            return true;
        }

        // Most end users don't know Regex, this function will convert basic wildcards to regex patterns.
        public static bool WildcardMatch( string input, string patternInput )
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
                .Replace( oldValue: @"\?", newValue: "."  );

            // Use regex to perform the wildcard matching
            return Regex.IsMatch( input, $"^{patternInput}$" );
        }


        [CanBeNull]
        public static string FixPathFormatting( [CanBeNull] string path )
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

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
        
        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates(DirectoryInfo dirInfo, bool includeSubFolders=true)
        {
            return FindCaseInsensitiveDuplicates(dirInfo?.FullName, includeSubFolders, isFile: false);
        }

        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates(FileInfo fileInfo)
        {
            // assumed Path.GetDirectoryName can't be null when passing a FileInfo's path.
            return FindCaseInsensitiveDuplicates(fileInfo?.DirectoryName, isFile: true);
        }

        // Finds all duplicate items in a path.
        public static IEnumerable<FileSystemInfo> FindCaseInsensitiveDuplicates(string path, bool includeSubFolders=true, bool? isFile=null)
        {
            string formattedPath = FixPathFormatting(path);
            if (formattedPath is null || !PathValidator.IsValidPath(formattedPath))
                throw new ArgumentException( $"'{path}' is not a valid path string" );

            // determine if path arg is a folder or a file.
            DirectoryInfo dirInfo;
            if (isFile == false)
            {
                dirInfo = new DirectoryInfo( formattedPath );
            }
            else if (isFile == true)
            {
                dirInfo = new DirectoryInfo(Path.GetDirectoryName(formattedPath));
            }
            else
            {
                dirInfo = new DirectoryInfo( formattedPath );
                if (!dirInfo.Exists)
                {
                    string folderPath = Path.GetDirectoryName(formattedPath);
                    isFile = true;
                    if ( !(folderPath is null ) )
                        dirInfo = new DirectoryInfo(folderPath);
                }
            }

            if (!dirInfo.Exists)
                throw new ArgumentException($"Path item doesn't exist on disk: '{formattedPath}'");

            // build duplicate files/folders list
            var fileList = new Dictionary<string, List<FileSystemInfo>>(StringComparer.OrdinalIgnoreCase);
            var folderList = new Dictionary<string, List<FileSystemInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo file in dirInfo.GetFiles())
            {
                if (!file.Exists)
                    continue;

                string filePath = file.FullName.ToLowerInvariant();
                if (!fileList.TryGetValue(filePath, out List<FileSystemInfo> files))
                {
                    files = new List<FileSystemInfo>();
                    fileList.Add(filePath, files);
                }
                files.Add(file);
            }
            
            foreach (KeyValuePair<string, List<FileSystemInfo>> fileListEntry in fileList)
            {
                List<FileSystemInfo> files = fileListEntry.Value;
                if (files.Count <= 1)
                    continue;

                foreach (FileSystemInfo duplicate in files)
                {
                    yield return duplicate;
                }
            }

            // don't iterate folders in the parent folder if original path is a file.
            if (isFile == true)
                yield break;

            foreach (DirectoryInfo subDirectory in dirInfo.GetDirectories())
            {
                if (!subDirectory.Exists)
                    continue;

                if (!folderList.TryGetValue(subDirectory.FullName.ToLowerInvariant(), out List<FileSystemInfo> folders))
                {
                    folders = new List<FileSystemInfo>();
                    folderList.Add(subDirectory.FullName.ToLowerInvariant(), folders);
                }
                folders.Add(subDirectory);

                if (includeSubFolders)
                {
                    foreach (FileSystemInfo duplicate in FindCaseInsensitiveDuplicates(subDirectory))
                    {
                        yield return duplicate;
                    }
                }

                foreach (KeyValuePair<string, List<FileSystemInfo>> folderListEntry in folderList)
                {
                    List<FileSystemInfo> foldersInCurrentDir = folderListEntry.Value;
                    if (foldersInCurrentDir.Count <= 1)
                        continue;

                    foreach (FileSystemInfo duplicate in foldersInCurrentDir)
                    {
                        yield return duplicate;
                    }
                }
                
                folderList.Clear();
            }
        }

        public static (FileSystemInfo, List<string>) GetClosestMatchingEntry( string path )
        {
            if ( !PathValidator.IsValidPath( path ) )
                throw new ArgumentException( nameof( path ) + " is not a valid path string" );

            path = FixPathFormatting(path);

            string directoryName = Path.GetDirectoryName( path );
            if ( string.IsNullOrEmpty(directoryName) )
            {
                return ( null, new List<string>() );
            }

            string searchPattern = Path.GetFileName( path );

            FileSystemInfo closestMatch = null;
            int maxMatchingCharacters = -1;
            var duplicatePaths = new List<string>();

            var directory = new DirectoryInfo( directoryName );
            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos(searchPattern, SearchOption.TopDirectoryOnly))
            {
                if (string.IsNullOrWhiteSpace(entry?.FullName))
                    continue;

                int matchingCharacters = GetMatchingCharactersCount(entry.FullName, path);
                if (matchingCharacters > maxMatchingCharacters)
                {
                    if (closestMatch != null)
                    {
                        duplicatePaths.Add(closestMatch.FullName);
                    }

                    closestMatch = entry;
                    maxMatchingCharacters = matchingCharacters;
                }
                else if (matchingCharacters != 0)
                {
                    duplicatePaths.Add(entry.FullName);
                }
            }

            return ( closestMatch, duplicatePaths );
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
                if (char.ToLowerInvariant(str1[i]) != char.ToLowerInvariant(str2[i]))
                    return 0;

                // increment matching count if case-sensitive match at this char index succeeds
                if ( str1[i] == str2[i] )
                    matchingCount++;
            }

            return matchingCount;
        }
    }
}
