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
        // we don't check colon or any slashes here, because we aren't validating file/folder names, only a full path string.
        private static readonly char[] s_invalidPathCharsWindows = {
	        '\0', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '!', '"', '$', '%', '&', '*', '+',
	        '<', '=', '>', '?', '@', '{', '}', '`', ',', '^',
        };


        // Characters not allowed in Unix file and directory names
        private static readonly char[] s_invalidPathCharsUnix = {
            '\0',
        };

        // Reserved file names in Windows
        private static readonly string[] s_reservedFileNamesWindows = {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        // Checks if the path is valid on running platform, or optionally (default) enforce for all platforms.
        public static bool IsValidPath(
	        [CanBeNull] string path,
	        bool enforceAllPlatforms = true,
	        bool ignoreWildcards = false
        )
        {
            try
            {
	            if ( string.IsNullOrWhiteSpace( path ) )
		            return false;

	            if ( HasMixedSlashes( path ) )
		            return false;

	            if ( HasRepeatedSlashes( path ) )
		            return false;

                // Check for forbidden os-specific ASCII characters
                char[] invalidChars = enforceAllPlatforms
                    ? s_invalidPathCharsWindows // already contains the unix ones
                    : GetInvalidCharsForPlatform();

                // should we ignore wildcards?
                invalidChars = ignoreWildcards
                    ? invalidChars.Where( c => c != '*' && c != '?' ).ToArray()
                    : invalidChars;

                if ( path.IndexOfAny( invalidChars ) >= 0 )
                    return false;

                // Check for non-printable characters
                if ( ContainsNonPrintableChars( path ) )
                    return false;

                // Check for reserved file names in Windows
                // ReSharper disable once InvertIf
                if ( enforceAllPlatforms || RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                {
	                if ( HasColonOutsideOfPathRoot( path ) )
		                return false;

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
                Logger.LogException( e );
                return false;
            }
        }


        public static bool HasColonOutsideOfPathRoot( [CanBeNull] string path )
        {
	        if ( string.IsNullOrWhiteSpace(path) ) return false;

	        string[] parts = path.Split( '/', '\\' );
	        for (int i = 1; i < parts.Length; i++)
	        {
		        if ( !parts[i].Contains( ":" ) )
			        continue;

		        return true; // Found a colon in a non-root part
	        }

	        return false;
        }


        public static bool HasRepeatedSlashes( [CanBeNull] string input )
        {
	        if ( string.IsNullOrWhiteSpace(input) ) return false;

	        for (int i = 0; i < input.Length - 1; i++)
	        {
		        if ( (input[i] == '\\' || input[i] == '/') && (input[i+1] == '\\' || input[i+1] == '/') )
			        return true;
	        }
	        return false;
        }

		
        public static char[] GetInvalidCharsForPlatform() =>
            Environment.OSVersion.Platform == PlatformID.Unix
                ? s_invalidPathCharsUnix
                : s_invalidPathCharsWindows;


        public static bool HasMixedSlashes( [CanBeNull] string input ) => ( input?.Contains('/') ?? false ) && input.Contains('\\');


        // This method checks whether any character's ascii code in the path is less than a space (ASCII code 32).
        public static bool ContainsNonPrintableChars( [CanBeNull] string path ) => path?.Any( c => c < ' ' ) ?? false;


        public static bool IsReservedFileNameWindows( [CanBeNull] string path )
        {
	        if ( string.IsNullOrWhiteSpace(path) ) return false;

	        string[] pathParts = path.Split( new[] { '\\', '/', Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries );

            return pathParts
                .Select( Path.GetFileNameWithoutExtension )
                .Any( fileName =>
                    s_reservedFileNamesWindows.Any(
                        reservedName => string.Equals(
                            reservedName,
                            fileName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                );
        }


        public static bool HasInvalidWindowsFileNameParts( [CanBeNull] string path )
        {
	        if ( string.IsNullOrEmpty(path) ) return false;

	        string[] pathParts = path.Split( new[] { '\\', '/', Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries );
	        foreach ( string part in pathParts )
	        {
		        // Check for a filename ending with a period or space
		        if ( part.EndsWith( " " ) || part.EndsWith( "." ) )
			        return true;

		        // Check for consecutive periods in the filename
		        for ( int i = 0; i < part.Length - 1; i++ )
		        {
			        if ( part[i] == '.' && part[i + 1] == '.' )
				        return true;
		        }
	        }

            return false;
        }
    }
}
