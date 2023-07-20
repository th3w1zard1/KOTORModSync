// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core.TSLPatcher
{
    public class IniHelper
    {
        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        // use PlaintextLog=1
        public static void ReplaceLookupGameFolder( [NotNull] DirectoryInfo directory )
        {
            if ( directory == null )
            {
                throw new ArgumentNullException( nameof( directory ) );
            }

            FileInfo[] iniFiles = directory.GetFiles( searchPattern: "*.ini", SearchOption.AllDirectories );
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
                fileContents = Regex.Replace( fileContents, pattern, replacement: "LookupGameFolder=0" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }

        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        // use PlaintextLog=1
        public static void ReplacePlaintextLog( [NotNull] DirectoryInfo directory )
        {
            if ( directory == null )
            {
                throw new ArgumentNullException( nameof( directory ) );
            }

            FileInfo[] iniFiles = directory.GetFiles( searchPattern: "*.ini", SearchOption.AllDirectories );
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
                fileContents = Regex.Replace( fileContents, pattern, replacement: "PlaintextLog=1" );

                // Write the modified file contents back to the file
                File.WriteAllText( filePath, fileContents );
            }
        }
    }
}
