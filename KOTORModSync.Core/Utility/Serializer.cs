// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

// ReSharper disable UnusedMember.Global
#pragma warning disable RCS1213, IDE0051, IDE0079

namespace KOTORModSync.Core.Utility
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public static class Serializer
    {
        public static void DeserializeGuidDictionary(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object value)) return;

            switch (value)
            {
                case string stringValue:
                    {
                        // Convert the string to a list of strings
                        List<string> stringList = new List<string>(65535) { stringValue };

                        // Replace the string value with the list
                        dict[key] = stringList;

                        // Fix GUID strings in each list item
                        for (int i = 0;
                             i < stringList.Count;
                             i++)
                        {
                            if (Guid.TryParse(stringList[i], out _)) continue;

                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString(stringList[i]);

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }

                        break;
                    }
                case List<string> stringList:
                    {
                        // Fix GUID strings in each list item
                        for (int i = 0;
                             i < stringList.Count;
                             i++)
                        {
                            if (Guid.TryParse(stringList[i], out _)) continue;

                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString(stringList[i]);

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }

                        break;
                    }
            }
        }

        [CanBeNull]
        private static string FixGuidString(string guidString)
        {
            // Remove any whitespace characters
            guidString = Regex.Replace(guidString, @"\s", "");

            // Attempt to fix common issues with GUID strings
            if (!guidString.StartsWith("{", StringComparison.Ordinal))
                guidString = "{" + guidString;
            if (!guidString.EndsWith("}", StringComparison.Ordinal)) guidString += "}";

            if (guidString.IndexOf('-') < 0)
            {
                guidString = Regex.Replace(
                    guidString,
                    @"(\w{8})(\w{4})(\w{4})(\w{4})(\w{12})",
                    "$1-$2-$3-$4-$5"
                );
            }

            return guidString;
        }

        public static void DeserializePath(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object pathValue)) return;

            switch (pathValue)
            {
                case string path:
                    {
                        string formattedPath = FixPathFormatting(path);
                        dict[key] = new List<string> { PrefixPath(formattedPath) };
                        break;
                    }
                case IList<string> paths:
                    {
                        for (int index = 0;
                             index < paths.Count;
                             index++)
                        {
                            string currentPath = paths[index];
                            string formattedPath = FixPathFormatting(currentPath);
                            paths[index] = PrefixPath(formattedPath);
                        }

                        break;
                    }
            }
        }

        public static string PrefixPath(string path) =>
            !path.StartsWith("<<modDirectory>>") && !path.StartsWith("<<kotorDirectory>>")
                ? "<<modDirectory>>" + path : path;

        public static string FixPathFormatting(string path)
        {
            // Replace backslashes with forward slashes
            string formattedPath = path.Replace('\\', '/');

            // Fix repeated slashes
            formattedPath = Regex.Replace(formattedPath, "(?<!:)//+", "/");

            // Fix trailing slashes
            formattedPath = formattedPath.TrimEnd('/');

            return formattedPath;
        }

        public static Dictionary<string, object> ConvertTomlTableToDictionary(TomlTable tomlTable)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>(65535);

            foreach (KeyValuePair<string, object> kvp in tomlTable)
            {
                string key = kvp.Key.ToLowerInvariant();
                object value = kvp.Value;

                if (value is TomlTable nestedTable)
                    dict.Add(key, ConvertTomlTableToDictionary(nestedTable));
                else
                    dict.Add(key, value);
            }

            return dict;
        }

        public static string FixWhitespaceIssues(string tomlContents)
        {
            tomlContents = tomlContents.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = tomlContents.Split('\n').Select(line => line.Trim()).ToArray();
            return string.Join("\n", lines);
        }

        public static string GenerateModDocumentation(List<Component> componentsList)
        {
            StringBuilder sb = new StringBuilder(50000);
            const string indentation = "    ";

            // Loop through each 'thisMod' entry
            foreach (Component component in componentsList)
            {
                _ = sb.AppendLine();

                // Component Information
                _ = sb.Append("####**").Append(component.Name).AppendLine("**");
                _ = sb.Append("**Author**: ").AppendLine(component.Author);
                _ = sb.AppendLine();
                _ = sb.Append("**Description**: ").AppendLine(component.Description);
                _ = sb.Append("**Tier & Category**: ")
                    .Append(component.Tier)
                    .Append(" - ")
                    .AppendLine(component.Category);
                if (component.Language != null)
                {
                    _ = string.Equals(
                        component.Language.FirstOrDefault(),
                        "All",
                        StringComparison.OrdinalIgnoreCase
                    ) ? sb.AppendLine("**Supported Languages**: ALL") : sb
                        .Append("**Supported Languages**: [")
                        .Append(Environment.NewLine)
                        .Append(
                            string.Join(
                                $",{Environment.NewLine}",
                                component.Language.Select(item => $"{indentation}{item}")
                            )
                        )
                        .Append(Environment.NewLine)
                        .Append(']')
                        .AppendLine();
                }

                _ = sb.Append("**Directions**: ").AppendLine(component.Directions);

                // Instructions
                if (component.Instructions == null) continue;

                _ = sb.AppendLine();
                _ = sb.AppendLine("**Installation Instructions:");
                foreach (Instruction instruction in component.Instructions.Where(
                    instruction => instruction.Action != "extract"
                ))
                {
                    _ = sb.Append("**Action**: ").AppendLine(instruction.Action);
                    if (instruction.Action == "move")
                    {
                        _ = sb.Append("**Overwrite existing files?**: ")
                            .AppendLine(instruction.Overwrite ? "NO" : "YES");
                    }

                    if (instruction.Source != null)
                    {
                        string thisLine
                            = $"Source: [{Environment.NewLine}{string.Join($",{Environment.NewLine}", instruction.Source.Select(item => $"{indentation}{item}"))}{Environment.NewLine}]";

                        if (instruction.Action != "move")
                            thisLine = thisLine?.Replace("Source: ", "");

                        _ = sb.AppendLine(thisLine);
                    }

                    if (instruction.Destination != null && instruction.Action == "move")
                        _ = sb.Append("Destination: ").AppendLine(instruction.Destination);
                }
            }

            return sb.ToString();
        }

        public static List<object> MergeLists(IEnumerable<object> list1, IEnumerable<object> list2)
        {
            List<object> mergedList = new List<object>(65535);
            mergedList.AddRange(list1);
            mergedList.AddRange(list2);
            return mergedList;
        }

        public static IEnumerable<object> EnumerateDictionaryEntries(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current == null)
                    continue;

                DictionaryEntry entry = (DictionaryEntry)enumerator.Current;
                yield return new KeyValuePair<object, object>(entry.Key, entry.Value);
            }
        }

        public static object SerializeObject(object obj)
        {
            Type type = obj.GetType();

            // do nothing if it's already a simple type.
            if (obj is IConvertible || obj is IFormattable || obj is IComparable)
                return obj.ToString();

            // handle generic list types
            if (obj is IList objList)
                return SerializeList(objList);

            Dictionary<string, object> serializedProperties = new Dictionary<string, object>();

            IEnumerable<object> members;
            switch ( obj ) {

                // IDictionary types
                case IDictionary mainDictionary:
                    IEnumerator enumerator = mainDictionary.GetEnumerator();
                    members = EnumerateDictionaryEntries(enumerator);
                    break;

                // class instance types
                default:
                    members = type.GetMembers(
                        BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.DeclaredOnly
                    );
                    break;
            }

            foreach (object member in members)
            {
                object value = null;
                string memberName = null;

                switch ( member ) {
                    case KeyValuePair<object, object> dictionaryEntry:
                        memberName = dictionaryEntry.Key.ToString();
                        value = dictionaryEntry.Value;
                        break;
                    case PropertyInfo property
                        when property.CanRead
                        && !property.GetMethod.IsStatic
                        && !Attribute.IsDefined(property, typeof(JsonIgnoreAttribute))
                        && property.DeclaringType == obj.GetType():
                        {
                            value = property.GetValue(obj);
                            memberName = property.Name;
                            break;
                        }
                    case FieldInfo field
                        when !field.IsStatic
                        && !Attribute.IsDefined(field, typeof(JsonIgnoreAttribute)):
                        {
                            value = field.GetValue(obj);
                            memberName = field.Name;
                            break;
                        }
                }

                switch ( value )
                {
                    case null:
                        continue;
                    case string valueStr:
                        serializedProperties[memberName] = valueStr;
                        break;
                    case IDictionary dictionary:
                        {
                            TomlTable tomlTable = new TomlTable();

                            foreach (DictionaryEntry entry in dictionary)
                            {
                                string key = entry.Key.ToString();
                                object value2 = SerializeObject(entry.Value);
                                tomlTable.Add(key, value2);
                            }

                            serializedProperties[memberName] = tomlTable;

                            break;
                        }

                    case IList list:
                        {
                            serializedProperties[memberName] = SerializeList(list);
                            break;
                        }
                    default:
                        {
                            if (value.GetType().IsNested)
                            {
                                serializedProperties[memberName] = SerializeObject(value);
                                continue;
                            }

                            serializedProperties[memberName] = value.ToString();

                            break;
                        }
                }
            }

            if (serializedProperties.Count > 0)
                return serializedProperties;

            return obj.ToString();
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static TomlArray SerializeList(IList list)
        {
            TomlArray serializedList = new TomlArray();

            foreach (object item in list)
            {
                if (item == null)
                    continue;

                if (item.GetType().IsPrimitive || item is string)
                {
                    serializedList.Add(item.ToString());
                    continue;
                }

                if (item is IList nestedList)
                    serializedList.Add(SerializeList(nestedList));
                else
                    serializedList.Add(SerializeObject(item));
            }

            return serializedList;
        }

        public static bool IsNonClassEnumerable(object obj)
        {
            Type type = obj.GetType();

            // Check if the type is assignable from IEnumerable, not a string, and not a class instance
            bool isNonClassEnumerable = typeof(IEnumerable).IsAssignableFrom(type)
                && type != typeof(string)
                && (!type.IsClass || type.IsSealed || type.IsAbstract);

            // Check if the object is a custom type (excluding dynamic types and proxy objects)
            bool isCustomType = type.FullName != null && (type.Assembly.FullName.StartsWith("Dynamic")
                || type.FullName.Contains("__TransparentProxy"));
            isNonClassEnumerable &= !isCustomType;

            return isNonClassEnumerable;
        }

        // A guid can't be dynamically converted by Nett and Tomlyn, so we convert to string instead.
        private static bool TryParseGuidAsString(object obj, out string guidString)
        {
            Type objType = obj.GetType();
            if (obj is Guid guidValue)
            {
                guidString = guidValue.ToString();
                return true;
            }

            if (
                objType                 == typeof(string)
                && ((string)obj).Length <= 38 // A guid in string form is always less than or eq to 38 chars.
                && Guid.TryParse(obj.ToString(), out Guid guidConvertedValue))
            {
                guidString = guidConvertedValue.ToString();
                return true;
            }

            guidString = null;
            return false;
        }

        private static bool IsEnumerable(object obj) => obj is IEnumerable enumerable && !(enumerable is string);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public static class FileHelper
    {
        public static string ResourcesDirectory
        {
            get
            {
                string outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            }
        }

        [CanBeNull]
        public static string GetFolderName(string itemInArchivePath)
            => Path.HasExtension(itemInArchivePath)
                ? Path.GetDirectoryName(itemInArchivePath)
                : itemInArchivePath;

        // Stop TSLPatcher from automatically assuming the KOTOR directory.
        public static void ReplaceLookupGameFolder(DirectoryInfo directory)
        {
            FileInfo[] iniFiles = directory.GetFiles("*.ini", SearchOption.AllDirectories);
            if (iniFiles.Length == 0)
                throw new InvalidOperationException("No .ini files found!");

            foreach (FileInfo file in iniFiles)
            {
                string filePath = file.FullName;
                string fileContents = File.ReadAllText(filePath);

                // Create a regular expression pattern to match "LookupGameFolder=1" with optional whitespace
                const string pattern = @"LookupGameFolder\s*=\s*1";

                // Use Regex.IsMatch to check if the pattern exists in the file contents
                if (! Regex.IsMatch(fileContents, pattern))
                    continue;

                // Use Regex.Replace to replace the pattern with "LookupGameFolder=0" (ignoring whitespace)
                fileContents = Regex.Replace(fileContents, pattern, "LookupGameFolder=0");

                // Write the modified file contents back to the file
                File.WriteAllText(filePath, fileContents);
            }
        }

        public static void OutputConfigFile(IEnumerable<Component> components, string filePath)
        {
            StringBuilder stringBuilder = new StringBuilder(65535);

            foreach (Component thisComponent in components)
                _ = stringBuilder.AppendLine(thisComponent.SerializeComponent());

            string tomlString = stringBuilder.ToString();
            File.WriteAllText(filePath, tomlString);
        }

        public static async Task MoveFileAsync(string sourcePath, string destinationPath)
        {
            using (FileStream sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true))
            {
                using (FileStream destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }
            }

            // The file is closed at this point, so it can be safely deleted
            File.Delete(sourcePath);
        }

        [CanBeNull]
        public static Component DeserializeTomlComponent(string tomlString)
        {
            tomlString = Serializer.FixWhitespaceIssues(tomlString);

            // Can't be bothered to find a real fix when this works fine.
            tomlString = tomlString.Replace("Instructions = []", "");
            tomlString = tomlString.Replace("Options = []", "");

            // Parse the TOML syntax into a TomlTable
            DocumentSyntax tomlDocument = Toml.Parse(tomlString);

            // Print any errors on the syntax
            if (tomlDocument.HasErrors)
            {
                foreach (DiagnosticMessage message in tomlDocument.Diagnostics)
                    Logger.Log(message.Message);

                return null;
            }

            TomlTable tomlTable = tomlDocument.ToModel();

            // Get the array of Component tables

            Component component = new Component();

            // Deserialize each TomlTable into a Component object
            if (!(tomlTable["thisMod"] is TomlTableArray componentTables))
                return component;

            foreach (TomlTable tomlComponent in componentTables)
                component.DeserializeComponent(tomlComponent);

            return component;
        }

        public static List<string> EnumerateFilesWithWildcards(IEnumerable<string> filesAndFolders, bool topLevelOnly = false)
        {
            List<string> result = new List<string>();

            HashSet<string> uniquePaths = new HashSet<string>(filesAndFolders);

            foreach (string path in uniquePaths.Where(path => ! string.IsNullOrEmpty(path)) )
                try
                {
                    string backslashPath = path
                        .Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);

                    if (!ContainsWildcards(backslashPath))
                    {
                        // Handle non-wildcard paths
                        if (File.Exists(backslashPath))
                        {
                            result.Add(backslashPath);
                            continue;
                        }

                        if (! Directory.Exists(backslashPath))
                            continue;

                        IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                            backslashPath,
                            "*",
                            topLevelOnly
                                ? SearchOption.TopDirectoryOnly
                                : SearchOption.AllDirectories
                        );

                        result.AddRange(matchingFiles);
                        continue;
                    }

                    // Handle wildcard paths
                    string directory = Path.GetDirectoryName(backslashPath);

                    if (!string.IsNullOrEmpty(directory)
                        && directory.IndexOfAny(Path.GetInvalidPathChars()) != -1
                        && Directory.Exists(directory))
                    {
                        IEnumerable<string> matchingFiles = Directory.EnumerateFiles(
                            directory,
                            Path.GetFileName(backslashPath),
                            topLevelOnly ? SearchOption.TopDirectoryOnly
                                : SearchOption.AllDirectories
                        );

                        result.AddRange(matchingFiles);
                        continue;
                    }

                    // Handle wildcard paths
                    string currentDirectory = backslashPath;

                    while (ContainsWildcards(currentDirectory))
                    {
                        string parentDirectory = Path.GetDirectoryName(currentDirectory);
                        if (string.IsNullOrEmpty(parentDirectory) || parentDirectory == currentDirectory)
                            break; // Exit the loop if no parent directory is found or if the parent directory is the same as the current directory

                        currentDirectory = parentDirectory;
                    }

                    if (string.IsNullOrEmpty(currentDirectory) || ! Directory.Exists(currentDirectory))
                        continue;

                    IEnumerable<string> checkFiles = Directory.EnumerateFiles(
                        currentDirectory,
                        "*",
                        topLevelOnly
                            ? SearchOption.TopDirectoryOnly
                            : SearchOption.AllDirectories
                    );

                    result.AddRange(checkFiles.Where(thisFile => WildcardMatch(thisFile, backslashPath)));
                }
                catch (Exception ex)
                {
                    // Handle or log the exception as required
                    Console.WriteLine($"An error occurred while processing path '{path}': {ex.Message}");
                }

            return result;
        }

        private static bool ContainsWildcards(string path) => path.Contains('*') || path.Contains('?');

        public static bool WildcardMatch(string input, string patternInput)
        {
            // Remove trailing slashes.
            input = input.TrimEnd('\\', '/');
            patternInput = patternInput.TrimEnd('\\', '/');

            // Split the input and pattern into directory levels
            string[] inputLevels = input.Split(Path.DirectorySeparatorChar);
            string[] patternLevels = patternInput.Split(Path.DirectorySeparatorChar);

            // Ensure the number of levels match
            if (inputLevels.Length != patternLevels.Length)
                return false;

            // Iterate over each level and perform wildcard matching
            for (int i = 0; i < inputLevels.Length; i++)
            {
                string inputLevel = inputLevels[i];
                string patternLevel = patternLevels[i];

                // Check if the current level is a wildcard
                if (patternLevel == "*" || patternLevel == "?")
                    continue;

                // Check if the current level matches the pattern
                if (!WildcardMatchLevel(inputLevel, patternLevel))
                    return false;
            }

            return true;
        }

        private static bool WildcardMatchLevel(string input, string pattern)
        {
            // Escape special characters in the pattern
            pattern = Regex.Escape(pattern);

            // Replace * with .* and ? with . in the pattern
            pattern = pattern
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");

            // Use regex to perform the wildcard matching
            return Regex.IsMatch(input, $"^{pattern}$");
        }

        public static List<Component> ReadComponentsFromFile(string filePath)
        {
            try
            {
                // Read the contents of the file into a string
                string tomlString = File.ReadAllText(filePath)
                    // the code expects instructions to always be defined. When it's not, this happens on save. Then our code errors when it sees this.
                    // make the user experience better by just removing an empty instructions key.
                    .Replace("Instructions = []", "");

                tomlString = Serializer.FixWhitespaceIssues(tomlString);

                // Parse the TOML syntax into a TomlTable
                DocumentSyntax tomlDocument = Toml.Parse(tomlString);

                // Print any errors on the syntax
                if (tomlDocument.HasErrors)
                {
                    foreach (DiagnosticMessage message in tomlDocument.Diagnostics)
                        Logger.LogException(new Exception(message.Message));
                }

                TomlTable tomlTable = tomlDocument.ToModel();

                // Get the array of Component tables
                TomlTableArray componentTables = tomlTable["thisMod"] as TomlTableArray;

                List<Component> components = new List<Component>(65535);
                foreach ((TomlObject tomlComponent, Component component) in
                    // Deserialize each TomlTable into a Component object
                    from TomlObject tomlComponent in componentTables
                    let component = new Component()
                    select (tomlComponent, component))
                {
                    component.DeserializeComponent(tomlComponent);
                    components.Add(component);
                    if (component.Instructions == null)
                    {
                        Logger.Log($"{component.Name} is missing instructions");
                        continue;
                    }

                    foreach (Instruction instruction in component.Instructions)
                        instruction.SetParentComponent(component);
                }

                return components;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        public static bool IsPath(string value) => Path.IsPathRooted(value) || Regex.IsMatch(value, @"^[a-zA-Z]:\\");

        public static bool IsDirectoryWithName(object directory, string name)
            => directory is Dictionary<string, object> dict
                && dict.ContainsKey("Name")
                && dict["Name"] is string directoryName
                && directoryName.Equals(name, StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, object> CreateNewDirectory(string name, bool isDirectory) => new Dictionary<string, object>
        {
            { "Name", name },
            { "Type", isDirectory ? "directory" : "file" },
            { "Contents", new List<object>() }
        };
    }

    public static class ArchiveHelper
    {
        public static ExtractionOptions DefaultExtractionOptions = new ExtractionOptions
        {
            ExtractFullPath = false,
            Overwrite = true,
            PreserveFileTime = true
        };

        public static bool IsArchive(string extension) => extension == ".zip" || extension == ".rar" || extension == ".7z";

        public static IArchive OpenArchive(Stream stream, string archivePath)
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return SharpCompress.Archives.Zip.ZipArchive.Open(stream);

            if (archivePath.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                return RarArchive.Open(stream);

            if (archivePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                return SevenZipArchive.Open(stream);

            return null;
        }

        public static IArchive OpenArchive(string archivePath)
        {
            FileStream stream = File.OpenRead(archivePath);
            if (archivePath.EndsWith(".zip"))
                return SharpCompress.Archives.Zip.ZipArchive.Open(stream);

            if (archivePath.EndsWith(".rar"))
                return RarArchive.Open(stream);

            if (archivePath.EndsWith(".7z"))
                return SevenZipArchive.Open(stream);

            // Close the stream if it wasn't returned as an archive
            stream?.Dispose();

            return null;
        }

        public static void OutputModTree(DirectoryInfo directory, string outputPath)
        {
            Dictionary<string, object> root = GenerateArchiveTreeJson(directory);
            try
            {
                string json = JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                File.WriteAllText(outputPath, json);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                Logger.Log($"Error writing output file {outputPath}: {ex.Message}");
            }
        }

        public static Dictionary<string, object> GenerateArchiveTreeJson(DirectoryInfo directory)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(65535) { { "Name", directory.Name }, { "Type", "directory" }, { "Contents", new List<object>() } };

            try
            {
                foreach (FileInfo file in directory.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                {
                    if (!IsArchive(file.Extension))
                        continue;

                    Dictionary<string, object> fileInfo = new Dictionary<string, object>(65535) { { "Name", file.Name }, { "Type", "file" } };
                    List<ModDirectory.ArchiveEntry> archiveEntries = TraverseArchiveEntries(file.FullName);
                    Dictionary<string, object> archiveRoot = new Dictionary<string, object>(65535) { { "Name", file.Name }, { "Type", "directory" }, { "Contents", archiveEntries } };

                    fileInfo["Contents"] = archiveRoot["Contents"];

                    (root["Contents"] as List<object>)?.Add(fileInfo);
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

        public static List<ModDirectory.ArchiveEntry> TraverseArchiveEntries(string archivePath)
        {
            List<ModDirectory.ArchiveEntry> archiveEntries = new List<ModDirectory.ArchiveEntry>(65535);

            try
            {
                IArchive archive = OpenArchive(archivePath);
                if (archive == null)
                {
                    Logger.Log($"Unsupported archive format: {Path.GetExtension(archivePath)}");
                    return archiveEntries;
                }

                archiveEntries.AddRange(
                    from entry in archive.Entries.Where(e => !e.IsDirectory)
                    let pathParts = entry.Key.Split(
                        archivePath.EndsWith(".rar")
                            ? '\\' // Use backslash as separator for RAR files
                            : '/'  // Use forward slash for other archive types
                    )
                    select new ModDirectory.ArchiveEntry { Name = pathParts[pathParts.Length - 1], Path = entry.Key }
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading archive {archivePath}: {ex.Message}");
            }

            return archiveEntries;
        }

        public static void ProcessArchiveEntry(IArchiveEntry entry, Dictionary<string, object> currentDirectory)
        {
            string[] pathParts = entry.Key.Split('/');
            bool isFile = !entry.IsDirectory;

            foreach (string name in pathParts)
            {
                List<object> existingDirectory = currentDirectory["Contents"] as List<object>
                    ?? throw new InvalidDataException($"Unexpected data type for directory contents: {currentDirectory["Contents"]?.GetType()}");

                string name1 = name;
                object existingChild = existingDirectory.Find(c => c is Dictionary<string, object> && FileHelper.IsDirectoryWithName(c, name1));

                if (existingChild != null)
                {
                    if (isFile)
                        ((Dictionary<string, object>)existingChild)["Type"] = "file";

                    currentDirectory = (Dictionary<string, object>)existingChild;
                }
                else
                {
                    Dictionary<string, object> child = new Dictionary<string, object>(65535) { { "Name", name }, { "Type", isFile ? "file" : "directory" }, { "Contents", new List<object>() } };
                    existingDirectory.Add(child);
                    currentDirectory = child;
                }
            }
        }
    }
}