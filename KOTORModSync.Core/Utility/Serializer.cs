// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
using static KOTORModSync.Core.ModDirectory;

namespace KOTORModSync.Core.Utility
{
    public static class Serializer
    {
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
                foreach (FileInfo file in directory.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                {
                    if (ArchiveHelper.IsArchive(file.Extension))
                    {
                        var fileInfo = new Dictionary<string, object>
                        {
                            { "Name", file.Name },
                            { "Type", "file" }
                        };
                        List<ArchiveEntry> archiveEntries = ArchiveHelper.TraverseArchiveEntries(file.FullName);
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

        public static class FileHandler
        {
            private static List<object> MergeLists(List<object> list1, List<object> list2)
            {
                var mergedList = new List<object>();
                mergedList.AddRange(list1);
                mergedList.AddRange(list2);
                return mergedList;
            }

            public static void OutputConfigFile(List<Component> components, string filePath)
            {
                var tomlTableArray = new TomlTableArray();
                var rootDictionary = new Dictionary<string, TomlTableArray>
                {
                    { "thisMod", tomlTableArray }
                };

                foreach (Component thisMod in components)
                {
                    var modTable = new TomlTable();

                    foreach (var property in typeof(Component).GetProperties())
                    {
                        string propertyName = property.Name;
                        var propertyValue = property.GetValue(thisMod);

                        if (propertyValue is IList list)
                        {
                            var indexedValues = new List<object>();

                            for (int i = 0; i < list.Count; i++)
                            {
                                indexedValues.Add(list[i]);
                            }

                            modTable.Add(propertyName, indexedValues);
                        }
                        else if (propertyValue != null && !property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
                        {
                            var nestedTable = GenerateNestedTable(propertyValue, new HashSet<object>());
                            modTable.Add(propertyName, nestedTable);
                        }
                        else
                        {
                            modTable.Add(propertyName, propertyValue);
                        }
                    }

                    tomlTableArray.Add(modTable);
                }

                var tomlString = Toml.FromModel(rootDictionary);

                // Log the generated TOML content
                Logger.Log(tomlString);

                File.WriteAllText(filePath, tomlString);
            }


            private static object GenerateNestedTable(object obj, HashSet<object> visitedObjects)
            {
                if (obj == null)
                {
                    return null;
                }

                // Check if the object has already been visited to avoid infinite recursion
                if (visitedObjects.Contains(obj))
                {
                    return null; // Or throw an exception, depending on your desired behavior
                }
                visitedObjects.Add(obj);

                Type objectType = obj.GetType();

                if (objectType.IsPrimitive || objectType == typeof(string) || objectType == typeof(DateTime))
                {
                    return obj;
                }

                if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var indexedValues = new List<object>();
                    IList list = (IList)obj;

                    for (int i = 0; i < list.Count; i++)
                    {
                        indexedValues.Add(GenerateNestedTable(list[i], visitedObjects));
                    }

                    return indexedValues;
                }

                if (objectType.IsArray)
                {
                    var indexedValues = new List<object>();
                    Array array = (Array)obj;

                    for (int i = 0; i < array.Length; i++)
                    {
                        indexedValues.Add(GenerateNestedTable(array.GetValue(i), visitedObjects));
                    }

                    return indexedValues;
                }

                var nestedTable = new TomlTable();

                foreach (var property in objectType.GetProperties())
                {
                    string propertyName = property.Name;
                    var propertyValue = property.GetValue(obj);

                    nestedTable.Add(propertyName, GenerateNestedTable(propertyValue, visitedObjects));
                }

                return nestedTable;
            }






            public static List<Component> ReadComponentsFromFile(string filePath)
            {
                try
                {
                    // Read the contents of the file into a string
                    string tomlString = File.ReadAllText(filePath);

                    // Parse the TOML syntax into a TomlTable
                    DocumentSyntax tomlDocument = Toml.Parse(tomlString);

                    // Print any errors on the syntax
                    if (tomlDocument.HasErrors)
                    {
                        foreach (var message in tomlDocument.Diagnostics)
                        {
                            Logger.LogException(new Exception(message.Message));
                        }
                    }

                    TomlTable tomlTable = tomlDocument.ToModel();

                    // Get the array of Component tables
                    TomlTableArray componentTables = tomlTable["thisMod"] as TomlTableArray;

                    List<Component> components = new List<Component>();

                    // Deserialize each TomlTable into a Component object
                    foreach (TomlObject tomlComponent in componentTables)
                    {
                        Component component = Component.DeserializeComponent(tomlComponent);
                        components.Add(component);

                        foreach (Instruction instruction in component.Instructions)
                        {
                            instruction.ParentComponent = component;
                        }
                    }

                    return components;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
                return null;
            }

            private static bool IsPath(string value)
            {
                return Path.IsPathRooted(value) || Regex.IsMatch(value, @"^[a-zA-Z]:\\");
            }

            public static bool IsDirectoryWithName(object directory, string name) => directory is Dictionary<string, object> dict &&
                    dict.ContainsKey("Name") &&
                    dict["Name"] is string directoryName &&
                    directoryName.Equals(name, StringComparison.OrdinalIgnoreCase);

            private static Dictionary<string, object> CreateNewDirectory(string name, bool isDirectory) => new Dictionary<string, object>
                {
                    { "Name", name },
                    { "Type", isDirectory ? "directory" : "file" },
                    { "Contents", new List<object>() }
                };
        }

        public static class ArchiveHelper
        {
            public static void OutputModTree(DirectoryInfo directory, string outputPath)
            {
                Dictionary<string, object> root = GenerateArchiveTreeJson(directory);
                try
                {
                    string json = JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings
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

            public static bool IsArchive(string extension) => extension == ".zip" || extension == ".rar" || extension == ".7z";

            public static List<ArchiveEntry> TraverseArchiveEntries(string archivePath)
            {
                var archiveEntries = new List<ArchiveEntry>();

                try
                {
                    using (FileStream stream = File.OpenRead(archivePath))
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

                        foreach (IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory))
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
                string[] pathParts = entry.Key.Split('/');
                bool isFile = !entry.IsDirectory;

                for (int i = 0; i < pathParts.Length; i++)
                {
                    string name = pathParts[i];

                    List<object> existingDirectory = currentDirectory["Contents"] as List<object>
                        ?? throw new InvalidDataException($"Unexpected data type for directory contents: {currentDirectory["Contents"]?.GetType()}");

                    object existingChild = existingDirectory.FirstOrDefault(c => c is Dictionary<string, object> && FileHandler.IsDirectoryWithName(c, name));

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
        }
    }
}
