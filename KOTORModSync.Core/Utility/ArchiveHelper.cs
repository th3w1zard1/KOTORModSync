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
            PreserveFileTime = true
        };

        public static bool IsArchive( string filePath ) => IsArchive( new FileInfo( filePath ) );

        public static bool IsArchive( FileInfo thisFile )
        {
            if ( // speeds up execution to do these checks rather than throw exceptions
                thisFile.Extension.Equals( ".zip" )
                || thisFile.Extension.Equals( ".7z" )
                || thisFile.Extension.Equals( ".rar" )
                || thisFile.Extension.Equals( ".exe" ) // assume self-extracting executable?
               )
            {
                return true;
            }

            return false;
        }

        [CanBeNull]
        public static IArchive OpenArchive( string archivePath )
        {
            try
            {
                IArchive archive = null;
                using ( FileStream stream = File.OpenRead( archivePath ) )
                {
                    if ( archivePath.EndsWith( ".zip" ) )
                    {
                        archive = SharpCompress.Archives.Zip.ZipArchive.Open( stream );
                    }
                    else if ( archivePath.EndsWith( ".rar" ) )
                    {
                        archive = RarArchive.Open( stream );
                    }
                    else if ( archivePath.EndsWith( ".7z" ) )
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


        public static void OutputModTree( DirectoryInfo directory, string outputPath )
        {
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
        public static Dictionary<string, object> GenerateArchiveTreeJson( DirectoryInfo directory )
        {
            var root = new Dictionary<string, object>( 65535 )
            {
                { "Name", directory.Name }, { "Type", "directory" }, { "Contents", new List<object>() }
            };

            try
            {
                foreach ( FileInfo file in directory.EnumerateFiles( "*.*", SearchOption.TopDirectoryOnly ) )
                {
                    if ( !IsArchive( file.Extension ) )
                    {
                        continue;
                    }

                    var fileInfo
                        = new Dictionary<string, object>( 65535 ) { { "Name", file.Name }, { "Type", "file" } };
                    List<ModDirectory.ArchiveEntry> archiveEntries = TraverseArchiveEntries( file.FullName );
                    var archiveRoot = new Dictionary<string, object>( 65535 )
                    {
                        { "Name", file.Name }, { "Type", "directory" }, { "Contents", archiveEntries }
                    };

                    fileInfo["Contents"] = archiveRoot["Contents"];

                    ( root["Contents"] as List<object> )?.Add( fileInfo );
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

        public static List<ModDirectory.ArchiveEntry> TraverseArchiveEntries( string archivePath )
        {
            var archiveEntries = new List<ModDirectory.ArchiveEntry>( 65535 );

            try
            {
                IArchive archive = OpenArchive( archivePath );
                if ( archive == null )
                {
                    Logger.Log( $"Unsupported archive format: '{Path.GetExtension( archivePath )}'" );
                    return archiveEntries;
                }

                archiveEntries.AddRange(
                    from entry in archive.Entries.Where( e => !e.IsDirectory )
                    let pathParts = entry.Key.Split(
                        archivePath.EndsWith( ".rar" )
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

        public static void ProcessArchiveEntry( IArchiveEntry entry, Dictionary<string, object> currentDirectory )
        {
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
                    c => c is Dictionary<string, object> && FileHelper.IsDirectoryWithName( c, name1 )
                );

                if ( existingChild != null )
                {
                    if ( isFile )
                    {
                        ( (Dictionary<string, object>)existingChild )["Type"] = "file";
                    }

                    currentDirectory = (Dictionary<string, object>)existingChild;
                }
                else
                {
                    var child = new Dictionary<string, object>( 65535 )
                    {
                        { "Name", name },
                        { "Type", isFile ? "file" : "directory" },
                        { "Contents", new List<object>() }
                    };
                    existingDirectory.Add( child );
                    currentDirectory = child;
                }
            }
        }
    }
}
