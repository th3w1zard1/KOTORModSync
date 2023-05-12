using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static KOTOR2ModSync.Core.ModDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using KOTORModSync.Core;

namespace KOTOR2ModSync.Core
{
    public class MainConfig
    {
        private static DirectoryInfo _sourcePath;
        private static DirectoryInfo _destinationPath;
        private static List<Component> _components;

        public static DirectoryInfo SourcePath => _sourcePath;
        public static DirectoryInfo DestinationPath => _destinationPath;
        public static List<Component> Components => _components;

        public static DirectoryInfo LastOutputDirectory;
        public static DirectoryInfo ModConfigPath;

        public void UpdateConfig(DirectoryInfo sourcePath, DirectoryInfo destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
        }

        public static void OutputModTree(DirectoryInfo directory, string outputPath)
        {
            Dictionary<string, object> root = GenerateArchiveTreeJson(directory);
            try
            {
                var json = JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                File.WriteAllText(outputPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing output file {outputPath}: {ex.Message}");
            }
        }

        private static Dictionary<string, object> GenerateArchiveTreeJson(DirectoryInfo directory)
        {
            var root = new Dictionary<string, object>
            {
                { "Name", directory.Name },
                { "Type", "directory" },
                { "Contents", new List<object>() }
            };

            try
            {
                foreach (var file in directory.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                {
                    if (IsArchive(file.Extension))
                    {
                        var fileInfo = new Dictionary<string, object>
                        {
                            { "Name", file.Name },
                            { "Type", "file" }
                        };
                        var archiveEntries = TraverseArchiveEntries(file.FullName);
                        var archiveRoot = new Dictionary<string, object>
                        {
                            { "Name", file.Name },
                            { "Type", "directory" },
                            { "Contents", archiveEntries }
                        };

                        fileInfo["Contents"] = archiveRoot["Contents"];


                        (root["Contents"] as List<object>).Add(fileInfo);
                    }
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating archive tree for {directory.FullName}: {ex.Message}");
                return null;
            }
            return root;
        }

        private static List<object> MergeLists(List<object> list1, List<object> list2)
        {
            var mergedList = new List<object>();
            mergedList.AddRange(list1);
            mergedList.AddRange(list2);
            return mergedList;
        }

        private static bool IsArchive(string extension)
        {
            return extension == ".zip" || extension == ".rar" || extension == ".7z";
        }

        public static List<ArchiveEntry> TraverseArchiveEntries(string archivePath)
        {
            var archiveEntries = new List<ArchiveEntry>();

            try
            {
                using (var stream = File.OpenRead(archivePath))
                {
                    IArchive archive = null;

                    if (archivePath.EndsWith(".zip"))
                    {
                        archive = SharpCompress.Archives.Zip.ZipArchive.Open(stream);
                    }
                    else if (archivePath.EndsWith(".rar"))
                    {
                        archive = RarArchive.Open(stream);
                    }
                    else if (archivePath.EndsWith(".7z"))
                    {
                        archive = SevenZipArchive.Open(stream);
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported archive format: {Path.GetExtension(archivePath)}");
                        return archiveEntries;
                    }

                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        string[] pathParts;
                        if (archivePath.EndsWith(".rar"))
                        {
                            pathParts = entry.Key.Split('\\'); // Use backslash as separator for RAR files
                        }
                        else
                        {
                            pathParts = entry.Key.Split('/'); // Use forward slash as separator for ZIP and 7z files
                        }
                        var archiveEntry = new ArchiveEntry { Name = pathParts[pathParts.Length - 1], Path = entry.Key };
                        archiveEntries.Add(archiveEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading archive {archivePath}: {ex.Message}");
            }

            return archiveEntries;
        }

        private static void ProcessArchiveEntry(IArchiveEntry entry, Dictionary<string, object> currentDirectory)
        {
            var pathParts = entry.Key.Split('/');
            var isFile = !entry.IsDirectory;

            for (var i = 0; i < pathParts.Length; i++)
            {
                var name = pathParts[i];

                var existingDirectory = currentDirectory["Contents"] as List<object>
                    ?? throw new InvalidDataException($"Unexpected data type for directory contents: {currentDirectory["Contents"]?.GetType()}");

                var existingChild = existingDirectory.FirstOrDefault(c => c is Dictionary<string, object> && IsDirectoryWithName(c, name));

                if (existingChild != null)
                {
                    if (isFile)
                    {
                        ((Dictionary<string, object>)existingChild)["Type"] = "file";
                    }

                    currentDirectory = (Dictionary<string, object>)existingChild;
                }
                else
                {
                    var child = new Dictionary<string, object>
                    {
                        { "Name", name },
                        { "Type", isFile ? "file" : "directory" },
                        { "Contents", new List<object>() }
                    };
                    existingDirectory.Add(child);
                    currentDirectory = child;
                }
            }
        }

        private static bool IsDirectoryWithName(object directory, string name)
        {
            return directory is Dictionary<string, object> dict &&
                dict.ContainsKey("Name") &&
                dict["Name"] is string directoryName &&
                directoryName.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object> CreateNewDirectory(string name, bool isDirectory)
        {
            return new Dictionary<string, object>
            {
                { "Name", name },
                { "Type", isDirectory ? "directory" : "file" },
                { "Contents", new List<object>() }
            };
        }

    }
    public static class ModDirectory
    {
        public class ArchiveEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public class ZipTree
        {
            public string Filename { get; set; }
            public string Name { get; set; }
            public bool IsFile { get; set; }
            public List<ZipTree> Children { get; set; } = new List<ZipTree>();
        }
    }
}
