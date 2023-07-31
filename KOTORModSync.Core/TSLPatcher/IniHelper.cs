// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using SharpCompress.Archives;

namespace KOTORModSync.Core.TSLPatcher
{
    public static class IniHelper
    {
        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        public static void ReplaceLookupGameFolder( [NotNull] DirectoryInfo directory )
        {
            if ( directory == null )
                throw new ArgumentNullException( nameof( directory ) );

            FileInfo[] iniFiles = directory.GetFiles( searchPattern: "*.ini", SearchOption.AllDirectories );
            if ( iniFiles.Length == 0 )
                throw new InvalidOperationException( "No .ini files found!" );

            foreach ( FileInfo file in iniFiles )
            {
                string filePath = file.FullName;
                string fileContents = File.ReadAllText( filePath );

                // Create a regular expression pattern to match "LookupGameFolder=1" with optional whitespace
                const string pattern = @"LookupGameFolder\s*=\s*1";

                // Use Regex.IsMatch to check if the pattern exists in the file contents
                if ( !Regex.IsMatch( fileContents, pattern ) )
                    continue;

                Logger.Log( $"Preventing tslpatcher automatic game lookups '{file.Name}'" );
                Logger.LogVerbose( $"change 'LookupGameFolder' from 1 to 0 in '{file.Name}'" );
                fileContents = Regex.Replace( fileContents, pattern, replacement: "LookupGameFolder=0" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }
        
        // use PlaintextLog=1 for installlog.txt instead of installlog.rtf
        public static void ReplacePlaintextLog( [NotNull] DirectoryInfo directory )
        {
            if ( directory == null )
                throw new ArgumentNullException( nameof( directory ) );

            FileInfo[] iniFiles = directory.GetFiles( searchPattern: "*.ini", SearchOption.AllDirectories );
            if ( iniFiles.Length == 0 )
                throw new InvalidOperationException( "No .ini files found!" );

            foreach ( FileInfo file in iniFiles )
            {
                if ( file is null )
                    continue;

                string filePath = file.FullName;
                string fileContents = File.ReadAllText( filePath );

                // Create a regular expression pattern to match "PlaintextLog=0" with optional whitespace
                const string pattern = @"PlaintextLog\s*=\s*0";

                // Use Regex.IsMatch to check if the pattern exists in the file contents
                if ( !Regex.IsMatch( fileContents, pattern, RegexOptions.IgnoreCase ) )
                    continue;

                Logger.Log( $"Redirecting TSLPatcher logging from '{file.Name}' to 'installlog.txt'" );
                Logger.LogVerbose( $"change 'PlaintextLog' from 0 to 1 in '{file.Name}'" );
                fileContents = Regex.Replace( fileContents, pattern, replacement: "PlaintextLog=1" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }

        public static Dictionary<string, string> ReadNamespacesIniFromArchive(Stream archiveStream)
        {
            using (var archive = ArchiveFactory.Open(archiveStream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory && entry.Key.Contains("tslpatchdata"))
                    {
                        using (var reader = new StreamReader(entry.OpenEntryStream()))
                        {
                            return ParseNamespacesIni(reader);
                        }
                    }
                }
            }

            return null; // Folder 'tslpatchdata' or 'namespaces.ini' not found in the archive.
        }

        public static Dictionary<string, string> ParseNamespacesIni( [NotNull] StreamReader reader)
        {
            if ( reader is null )
                throw new ArgumentNullException( nameof( reader ) );

            var namespaces = new Dictionary<string, string>();
            string line;
            while ( (line = reader.ReadLine()) != null )
            {
                line = line.Trim();
                if ( string.IsNullOrWhiteSpace( line ) || !line.StartsWith( "Namespace" ) )
                    continue;

                int separatorIndex = line.IndexOf('=');
                if ( separatorIndex == -1 )
                    continue;

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();
                namespaces[key] = value;
            }

            return namespaces;
        }
    }
}
