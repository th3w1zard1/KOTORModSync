// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
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
        public static bool IsValidPath( [CanBeNull] string path, bool enforceAllPlatforms = true )
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
                if ( ContainsNonPrintableChars( path ) )
                    return false;

                // Check for reserved file names in Windows
                if ( enforceAllPlatforms || RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                {
                    if ( IsReservedFileNameWindows( path ) )
                        return false;

                    // Check for invalid filename parts
                    // ReSharper disable once ConvertIfStatementToReturnStatement
                    if ( HasInvalidWindowsFileNameParts( path ) )
                        return false;
                }

                return true;
            }
            catch ( Exception e )
            {
                Console.WriteLine( e );
                return false;
            }
        }

        public static char[] GetInvalidCharsForPlatform()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix
                ? s_invalidPathCharsUnix
                : s_invalidPathCharsWindows;
        }

        public static bool ContainsNonPrintableChars( [CanBeNull] string path ) => path?.Any( c => c < ' ' && c != '\t' ) ?? false;

        public static bool IsReservedFileNameWindows( string path )
        {
            string fileName = Path.GetFileNameWithoutExtension( path );

            // Check if any reserved filename matches the filename (case-insensitive)
            return s_reservedFileNamesWindows.Any( reservedName => string.Equals( reservedName, fileName, StringComparison.OrdinalIgnoreCase ) );
        }

        public static bool HasInvalidWindowsFileNameParts( string path )
        {
            string fileName = Path.GetFileNameWithoutExtension( path );

            // Check for a filename ending with a period or space
            if ( fileName.EndsWith( " " ) || fileName.EndsWith( "." ) )
                return true;

            // Check for consecutive periods in the filename
            for ( int i = 0; i < fileName.Length - 1; i++ )
            {
                if ( fileName[i] == '.' && fileName[i + 1] == '.' )
                    return true;
            }

            return false;
        }
    }
}
