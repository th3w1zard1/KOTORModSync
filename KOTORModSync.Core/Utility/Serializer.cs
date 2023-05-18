// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using Tomlyn;
using Tomlyn.Syntax;
using Tomlyn.Model;
using Nett;
using static KOTORModSync.Core.ModDirectory;
using System.ComponentModel;
using System.Text;

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
                Logger.Log($"Error generating archive tree for {directory.FullName}: {ex.Message}");
                return null;
            }
            return root;
        }

        public static string FixWhitespaceIssues(string tomlContents)
        {
            // Normalize line endings to '\n'
            tomlContents = tomlContents.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove leading and trailing whitespaces from each line
            string[] lines = tomlContents.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimStart().TrimEnd();
            }

            // Join the lines with '\n' separator
            tomlContents = string.Join("\n", lines);

            return tomlContents;
        }

        public static string SerializeComponent(Component component)
        {
            var rootTable = new Dictionary<string, List<object>>()
            {
                ["thisMod"] = new List<object>()
                    {
                        SerializeObject(component)
                    }
            };

            var config = TomlSettings.Create();
            var tomlString = Nett.Toml.WriteString(rootTable);
            return FixWhitespaceIssues(tomlString);
        }

        public static object SerializeObject(object obj)
        {
            var type = obj.GetType();
            var serializedProperties = new Dictionary<string, object>();

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = null;
                string memberName = null;

                if (member is PropertyInfo property && property.CanRead)
                {
                    value = property.GetValue(obj);
                    memberName = property.Name;
                }
                else if (member is FieldInfo field)
                {
                    value = field.GetValue(obj);
                    memberName = field.Name;
                }

                if (value != null)
                {
                    if (value is IEnumerable && !(value is string))
                    {
                        var serializedList = new Tomlyn.Model.TomlArray();

                        foreach (var item in (IEnumerable)value)
                        {
                            if (item != null && item.GetType().IsPrimitive || item is string)
                            {
                                if (item is IEnumerable && !(item is string))
                                    serializedList.Add(SerializeObject(item));
                                else
                                    serializedList.Add(item?.ToString());
                            }
                            else
                            {
                                serializedList.Add(SerializeObject(item));
                            }
                        }


                        serializedProperties[memberName] = serializedList;
                    }
                    else if (value.GetType().IsNested)
                    {
                        serializedProperties[memberName] = SerializeObject(value);
                    }
                    else if (value != null && value.GetType().IsPrimitive || value is string)
                    {
                        serializedProperties[memberName] = value?.ToString();
                    }
                }
            }

            if (serializedProperties.Count > 0)
            {
                return serializedProperties;
            }
            else if (!type.IsNested && serializedProperties.Count == 0)
            {
                return SerializeObject(obj);
            }

            return null;
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
                var stringBuilder = new StringBuilder(10000);

                foreach (var component in components)
                {
                    var serializedComponent = SerializeComponent(component); // Call SerializeComponent
                    stringBuilder.AppendLine(serializedComponent);
                }

                var tomlString = stringBuilder.ToString();
                File.WriteAllText(filePath, tomlString);
            }

            public static Component DeserializeTomlComponent(string tomlString)
            {
                tomlString = FixWhitespaceIssues(tomlString);

                // Parse the TOML syntax into a TomlTable
                DocumentSyntax tomlDocument = Tomlyn.Toml.Parse(tomlString);

                // Print any errors on the syntax
                if (tomlDocument.HasErrors)
                {
                    foreach (var message in tomlDocument.Diagnostics)
                    {
                        Logger.LogException(new Exception(message.Message));
                    }
                    return null;
                }

                Tomlyn.Model.TomlTable tomlTable = tomlDocument.ToModel();

                // Get the array of Component tables
                Tomlyn.Model.TomlTableArray componentTables = tomlTable["thisMod"] as Tomlyn.Model.TomlTableArray;

                Component component = null;

                // Deserialize each TomlTable into a Component object
                foreach (Tomlyn.Model.TomlObject tomlComponent in componentTables)
                {
                    component = Component.DeserializeComponent(tomlComponent);
                    if (component.Instructions != null)
                    {
                        foreach (Instruction instruction in component.Instructions)
                        {
                            instruction.ParentComponent = component;
                        }
                    }
                    break;
                }

                return component;
            }

            public static List<Component> ReadComponentsFromFile(string filePath)
            {
                try
                {
                    // Read the contents of the file into a string
                    string tomlString = File.ReadAllText(filePath);

                    tomlString = FixWhitespaceIssues(tomlString);

                    // Parse the TOML syntax into a TomlTable
                    DocumentSyntax tomlDocument = Tomlyn.Toml.Parse(tomlString);

                    // Print any errors on the syntax
                    if (tomlDocument.HasErrors)
                    {
                        foreach (var message in tomlDocument.Diagnostics)
                        {
                            Logger.LogException(new Exception(message.Message));
                        }
                    }

                    Tomlyn.Model.TomlTable tomlTable = tomlDocument.ToModel();

                    // Get the array of Component tables
                    Tomlyn.Model.TomlTableArray componentTables = tomlTable["thisMod"] as Tomlyn.Model.TomlTableArray;

                    List<Component> components = new List<Component>();

                    // Deserialize each TomlTable into a Component object
                    foreach (Tomlyn.Model.TomlObject tomlComponent in componentTables)
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
                    Logger.LogException(ex);
                    Logger.Log($"Error writing output file {outputPath}: {ex.Message}");
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
                            Logger.Log($"Unsupported archive format: {Path.GetExtension(archivePath)}");
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
                    Logger.Log($"Error reading archive {archivePath}: {ex.Message}");
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
