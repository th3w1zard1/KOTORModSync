// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace KOTORModSync.Core.Utility
{
    public static class ArchiveHelper
    {
        public static readonly ExtractionOptions DefaultExtractionOptions = new ExtractionOptions
        {
            ExtractFullPath = false,
            Overwrite = true,
            PreserveFileTime = true,
        };

        public static bool IsArchive( [NotNull] string filePath ) => IsArchive(
            new FileInfo( filePath ?? throw new ArgumentNullException( nameof( filePath ) ) )
        );

        public static bool IsArchive( [NotNull] FileInfo thisFile ) => thisFile.Extension.Equals( ".zip" )
                                                                       || thisFile.Extension.Equals( ".7z" )
                                                                       || thisFile.Extension.Equals( ".rar" )
                                                                       || thisFile.Extension.Equals( ".exe" );

        [CanBeNull]
        public static IArchive OpenArchive( [NotNull] string archivePath )
        {
            if ( archivePath == null )
            {
                throw new ArgumentNullException( nameof( archivePath ) );
            }

            try
            {
                IArchive archive = null;
                using ( FileStream stream = File.OpenRead( archivePath ) )
                {
                    if ( archivePath.EndsWith( ".zip", StringComparison.OrdinalIgnoreCase ) )
                    {
                        archive = SharpCompress.Archives.Zip.ZipArchive.Open( stream );
                    }
                    else if ( archivePath.EndsWith( ".rar", StringComparison.OrdinalIgnoreCase ) )
                    {
                        archive = RarArchive.Open( stream );
                    }
                    else if ( archivePath.EndsWith( ".7z", StringComparison.OrdinalIgnoreCase ) )
                    {
                        archive = SevenZipArchive.Open( stream );
                    }

                    return archive;
                }
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return null;
            }
        }

        public static void OutputModTree( [NotNull] DirectoryInfo directory, [NotNull] string outputPath )
        {
            if ( directory == null )
                throw new ArgumentNullException( nameof( directory ) );
            if ( outputPath == null )
                throw new ArgumentNullException( nameof( outputPath ) );

            Dictionary<string, object> root = GenerateArchiveTreeJson( directory );
            try
            {
                string json = JsonConvert.SerializeObject(
                    root,
                    Formatting.Indented,
                    new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
                );

                File.WriteAllText( outputPath, json );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, $"Error writing output file '{outputPath}': {ex.Message}" );
            }
        }

        [CanBeNull]
        public static Dictionary<string, object> GenerateArchiveTreeJson( [NotNull] DirectoryInfo directory )
        {
            if ( directory == null )
                throw new ArgumentNullException( nameof( directory ) );

            var root = new Dictionary<string, object>
            {
                { "Name", directory.Name }, { "Type", "directory" }, { "Contents", new List<object>() },
            };

            try
            {
                foreach ( FileInfo file in directory.EnumerateFiles( searchPattern: "*.*", SearchOption.TopDirectoryOnly ) )
                {
                    if ( file == null || !IsArchive( file.Extension ) )
                        continue;

                    var fileInfo
                        = new Dictionary<string, object> { { "Name", file.Name }, { "Type", "file" } };
                    List<ModDirectory.ArchiveEntry> archiveEntries = TraverseArchiveEntries( file.FullName );
                    var archiveRoot = new Dictionary<string, object>
                    {
                        { "Name", file.Name }, { "Type", "directory" }, { "Contents", archiveEntries },
                    };

                    fileInfo["Contents"] = archiveRoot["Contents"];

                    ( root["Contents"] as List<object> ).Add( fileInfo );
                }

                /*foreach (var subdirectory in directory.EnumerateDirectories())
                {
                    var subdirectoryInfo = new Dictionary<string, object>
                    {
                        { "Name", subdirectory.Name },
                        { "Type", "directory" },
                        { "Contents", GenerateArchiveTreeJson(subdirectory) }
                    };

                    (root["Contents"] as List<object>).Add(subdirectoryInfo);
                }*/
            }
            catch ( Exception ex )
            {
                Logger.Log( $"Error generating archive tree for '{directory.FullName}': {ex.Message}" );
                return null;
            }

            return root;
        }

        [NotNull]
        private static List<ModDirectory.ArchiveEntry> TraverseArchiveEntries( [NotNull] string archivePath )
        {
            if ( archivePath == null )
                throw new ArgumentNullException( nameof( archivePath ) );

            var archiveEntries = new List<ModDirectory.ArchiveEntry>(  );

            try
            {
                IArchive archive = OpenArchive( archivePath );
                if ( archive is null )
                {
                    Logger.Log( $"Unsupported archive format: '{Path.GetExtension( archivePath )}'" );
                    return archiveEntries;
                }

                archiveEntries.AddRange(
                    from entry in archive.Entries.Where( e => !e.IsDirectory )
                    let pathParts = entry.Key.Split(
                        archivePath.EndsWith( ".rar", StringComparison.OrdinalIgnoreCase )
                            ? '\\' // Use backslash as separator for RAR files
                            : '/' // Use forward slash for other archive types
                    )
                    select new ModDirectory.ArchiveEntry { Name = pathParts[pathParts.Length - 1], Path = entry.Key }
                );
            }
            catch ( Exception ex )
            {
                Logger.Log( $"Error reading archive '{archivePath}': {ex.Message}" );
            }

            return archiveEntries;
        }

        public static void ProcessArchiveEntry( [NotNull] IArchiveEntry entry, [NotNull] Dictionary<string, object> currentDirectory )
        {
            if ( entry == null )
                throw new ArgumentNullException( nameof(entry) );
            if ( currentDirectory == null )
                throw new ArgumentNullException( nameof(currentDirectory) );

            string[] pathParts = entry.Key.Split( '/' );
            bool isFile = !entry.IsDirectory;

            foreach ( string name in pathParts )
            {
                List<object> existingDirectory = currentDirectory["Contents"] as List<object>
                    ?? throw new InvalidDataException(
                        $"Unexpected data type for directory contents: '{currentDirectory["Contents"]?.GetType()}'"
                    );

                string name1 = name;
                object existingChild = existingDirectory.Find(
                    c => c is Dictionary<string, object> dict
                    && dict.ContainsKey( "Name" )
                    && dict["Name"] is string directoryName
                    && directoryName.Equals( name, StringComparison.OrdinalIgnoreCase )
                );

                if ( existingChild != null )
                {
                    if ( isFile )
                        ( (Dictionary<string, object>)existingChild )["Type"] = "file";

                    currentDirectory = (Dictionary<string, object>)existingChild;
                }
                else
                {
                    var child = new Dictionary<string, object>( )
                    {
                        { "Name", name },
                        {
                            "Type", isFile
                                ? "file"
                                : "directory"
                        },
                        { "Contents", new List<object>() },
                    };
                    existingDirectory.Add( child );
                    currentDirectory = child;
                }
            }
        }
    }
}
