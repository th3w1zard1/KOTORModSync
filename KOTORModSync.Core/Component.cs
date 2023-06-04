// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
using Nett;
using Tomlyn.Model;
using TomlObject = Tomlyn.Model.TomlObject;
using TomlTable = Tomlyn.Model.TomlTable;
using TomlTableArray = Tomlyn.Model.TomlTableArray;

namespace KOTORModSync.Core
{
    public class Component
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public string Tier { get; set; }
        public string Description { get; set; }
        public string Directions { get; set; }
        public List<string> Dependencies { get; private set; }
        public List<string> Restrictions { get; set; }
        public bool NonEnglishFunctionality { get; set; }
        public string InstallationMethod { get; set; }
        public List<Instruction> Instructions { get; set; }
        public List<string> Language { get; private set; }
        public int InstallOrder { get; set; }
        public string ModLink { get; set; }

        /*
        public DateTime SourceLastModified { get; internal set; }
        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        */

        public static readonly string DefaultComponent = @"
[[thisMod]]
    name = ""the name of your mod""
    # Use the button below to generate a Global Unique Identifier (guid) for this mod
    guid = ""{01234567-ABCD-EF01-2345-6789ABCDEF01}""
    # Copy and paste any guid of any mod you depend on here, format like below
    dependencies = [
        ""{d2bf7bbb-4757-4418-96bf-a9772a36a262}"",
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""
    ]
    # Copy and paste any guid of any incompatible mod here, format like below
    restrictions = [
        ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
        ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
    ]
    installOrder = 3";

        public string SerializeComponent()
        {
            _ = TomlSettings.Create();
            var rootTable = new Dictionary<string, List<object>>()
            {
                ["thisMod"] = new List<object>(65535)
                {
                    Serializer.SerializeObject(this)
                }
            };
            string tomlString = Nett.Toml.WriteString(rootTable);
            return Serializer.FixWhitespaceIssues(tomlString);
        }

        private DirectoryInfo _tempPath;

        public void DeserializeComponent(TomlObject tomlObject)
        {
            if (!(tomlObject is TomlTable componentTable))
            {
                throw new ArgumentException("Expected a TOML table for component data.");
            }

            _tempPath = new DirectoryInfo(Path.GetTempPath());

            Dictionary<string, object> componentDict = Utility.Serializer.ConvertTomlTableToDictionary(componentTable);
            if (componentDict.TryGetValue("path", out object pathValue) && pathValue is string path)
            {
                new List<string>(255).Add(path);
            }

            Serializer.DeserializePath(componentDict, "paths");
            this.Name = GetRequiredValue<string>(componentDict, "name");
            Logger.Log($"\r\n== Deserialize next component '{this.Name}' ==");
            this.Guid = GetRequiredValue<Guid>(componentDict, "guid");
            this.InstallOrder = GetValueOrDefault<int>(componentDict, "installorder");
            this.Description = GetValueOrDefault<string>(componentDict, "description");
            this.Directions = GetValueOrDefault<string>(componentDict, "directions");
            this.Category = GetValueOrDefault<string>(componentDict, "category");
            this.Tier = GetValueOrDefault<string>(componentDict, "tier");
            this.Language = GetValueOrDefault<List<string>>(componentDict, "language");
            this.Author = GetValueOrDefault<string>(componentDict, "author");
            this.Dependencies = GetValueOrDefault<List<string>>(componentDict, "dependencies");
            this.Restrictions = GetValueOrDefault<List<string>>(componentDict, "restrictions");
            this.ModLink = GetValueOrDefault<string>(componentDict, "modlink");

            this.Instructions = DeserializeInstructions(
                GetValueOrDefault<Tomlyn.Model.TomlTableArray>(
                    componentDict, "instructions")
            );

            if (this.Instructions.Count == 0)
                Logger.Log($"No instructions found for component {this.Name}");

            this.Instructions.ForEach(instruction => instruction.SetParentComponent(this));
            Logger.Log($"Successfully deserialized component '{this.Name}'\r\n");
        }

        [NotNull]
        private static List<Instruction> DeserializeInstructions([CanBeNull]TomlTableArray tomlObject)
        {
            if (tomlObject == null)
            {
                Logger.LogException(new Exception("Expected a TOML table array for instructions data."));
                return new List<Instruction>();
            }

            var instructions = new List<Instruction>(65535);

            for (int index = 0; index < tomlObject.Count; index++)
            {
                TomlTable item = tomlObject[index];
                Dictionary<string, object> instructionDict = Utility.Serializer.ConvertTomlTableToDictionary(item);

                // ConvertTomlTableToDictionary lowercase all string keys.
                Serializer.DeserializePath(instructionDict, "source");
                if (!instructionDict.ContainsKey("destination"))
                {
                    instructionDict["destination"] = "<<kotorDirectory>>/Override";
                }

                Serializer.DeserializeGuidDictionary(instructionDict, "restrictions");
                Serializer.DeserializeGuidDictionary(instructionDict, "dependencies");

                var instruction = new Instruction();
                instruction.Action = GetRequiredValue<string>(instructionDict, "action");
                Logger.Log($"\r\n-- Deserialize instruction #{index} action {instruction.Action}");
                instruction.Arguments = GetValueOrDefault<string>(instructionDict, "arguments");
                instruction.Overwrite = GetValueOrDefault<bool>(instructionDict, "overwrite");
                instruction.Restrictions = GetValueOrDefault<List<Guid>>(instructionDict, "restrictions");
                instruction.Dependencies = GetValueOrDefault<List<Guid>>(instructionDict, "dependencies");
                instruction.Source = GetValueOrDefault<List<string>>(instructionDict, "source");

                if (instruction.Action == "move")
                {
                    instruction.Destination = GetValueOrDefault<string>(instructionDict, "destination");
                }

                instructions.Add(instruction);
            }

            return instructions;
        }

        [CanBeNull] private static T GetRequiredValue<T>(IReadOnlyDictionary<string, object> dict, string key) => GetValue<T>(dict, key, true);
        [CanBeNull] private static T GetValueOrDefault<T>(IReadOnlyDictionary<string, object> dict, string key) => GetValue<T>(dict, key, false);

        private static T GetValue<T>(IReadOnlyDictionary<string, object> dict, string key, bool required)
        {
            if (!dict.TryGetValue(key, out object value))
            {
                string caseInsensitiveKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (caseInsensitiveKey == null)
                {
                    if (required)
                    {
                        Logger.LogException(new Exception($"Missing or invalid '{key}' field."));
                        return default;
                    }

                    return default;
                }

                value = dict[caseInsensitiveKey];
            }

            if (value is T t)
            {
                return t;
            }

            var targetType = value.GetType();

            if (value is string valueStr && typeof(T) == typeof(System.Guid)
                && System.Guid.TryParse(valueStr, out Guid guid))
                return (T)(object)guid;

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>) && value is IEnumerable enumerable)
            {
                Type elementType = typeof(T).GetGenericArguments()[0];
                dynamic dynamicList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

                foreach (object item in enumerable)
                {
                    dynamic convertedItem = Convert.ChangeType(item, elementType);
                    dynamicList.Add(convertedItem);
                }

                return dynamicList;
            }

            if ((value is string valueStr2 && string.IsNullOrEmpty(valueStr2)))
            {
                if (required)
                {
                    throw new ArgumentException($"'{key}' field is null or empty.");
                }

                return default;
            }

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
            {
                Type listType = typeof(T);
                Type elementType = listType.GetGenericArguments()[0];
                dynamic dynamicList = Activator.CreateInstance(listType);

                if (targetType.IsArray)
                {
                    var arrayValues = (Array)value;
                    foreach (var item in arrayValues)
                    {
                        dynamic convertedItem = Convert.ChangeType(item, elementType);
                        dynamicList.Add(convertedItem);
                    }
                }

                return dynamicList;
            }

            if (targetType == typeof(Tomlyn.Model.TomlArray) && value is Tomlyn.Model.TomlArray valueTomlArray)
            {
                if (valueTomlArray.Count == 0)
                    return default;

                Tomlyn.Model.TomlTableArray tomlTableArray = new TomlTableArray();

                foreach (var tomlValue in valueTomlArray)
                {
                    if (tomlValue is TomlTable table)
                    {
                        tomlTableArray.Add(table);
                    }
                    else
                    {
                        // Handle error or invalid item
                    }
                }

                return (T)(object)tomlTableArray;
            }


            try
            {
                T convertedValue = (T)Convert.ChangeType(value, typeof(T));
                return convertedValue;
            }
            catch (InvalidCastException)
            {
                if (required)
                {
                    throw new ArgumentException($"Invalid '{key}' field type.");
                }
            }
            catch (FormatException)
            {
                if (required)
                {
                    throw new ArgumentException($"Invalid format for '{key}' field.");
                }
            }

            return default;
        }

        private static bool IsEmptyValue(object value)
        {
            if (value == null)
            {
                return true;
            }

            var valueType = value.GetType();
            if (valueType.IsValueType)
            {
                return false; // Value types cannot be empty
            }

            // Handle additional cases for reference types
            // For example, check if it's an empty collection
            if (value is ICollection collection)
            {
                return collection.Count == 0;
            }

            // Add more checks here for other specific types as needed

            return false; // Default assumption: not empty
        }


        
        public async Task<(
                bool success,
                Dictionary<FileInfo, SHA1> originalChecksums)
            >
            ExecuteInstructions(
                Utility.Utility.IConfirmationDialogCallback confirmDialog,
                List<Component> componentsList
            )
        {
            try
            {
                // Check if we have permission to write to the Destination directory
                if (!Utility.Utility.CanWriteToDirectory(MainConfig.DestinationPath))
                    throw new InvalidOperationException("Cannot write to the destination directory.");

                (bool, Dictionary<FileInfo, SHA1>) result = await ProcessComponentAsync(this);
                if (result.Item1)
                    return (true, null);

                Logger.LogException(new Exception($"Component {Name} failed to install the mod correctly with {result}"));
                return (false, null);

                async Task<(bool, Dictionary<FileInfo, SHA1>)> ProcessComponentAsync(Component component)
                {
                    for (int i1 = 0; i1 < component.Instructions.Count; i1++)
                    {
                        Instruction instruction = component.Instructions[i1]
                            ?? throw new ArgumentException($"instruction null at index {i1}"
                                                           , nameof(componentsList));

                        //The instruction will run if any of the following conditions are met:
                        //The instruction has no dependencies or restrictions.
                        //The instruction has dependencies, and all of the required components are being installed.
                        //The instruction has restrictions, but none of the restricted components are being installed.
                        bool shouldRunInstruction = true;
                        if (instruction.Dependencies?.Count > 0)
                        {
                            shouldRunInstruction &= instruction.Dependencies.All(requiredGuid =>
                                componentsList.Any(checkComponent => checkComponent.Guid == requiredGuid));
                            if (!shouldRunInstruction)
                                Logger.Log($"Skipping instruction '{instruction.Action}' index {i1} due to missing dependency(s): {instruction.Dependencies}");
                        }

                        if (!shouldRunInstruction && instruction.Restrictions?.Count > 0)
                        {
                            shouldRunInstruction &= !instruction.Restrictions.Any(restrictedGuid =>
                                componentsList.Any(checkComponent => checkComponent.Guid == restrictedGuid));
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (!shouldRunInstruction)
                                Logger.Log($"Not running instruction {instruction.Action} index {i1} due to restricted components installed: {instruction.Restrictions}");
                        }

                        if (!shouldRunInstruction)
                            continue;

                        // Get the original check-sums before making any modifications
                        /*Logger.Log("Calculating game install file hashes");
                    var originalPathsToChecksum = MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories)
                        .ToDictionary(file => file, file => FileChecksumValidator.CalculateSHA1Async(file).Result);

                    if (instruction.OriginalChecksums == null)
                    {
                        instruction.OriginalChecksums = originalPathsToChecksum;
                    }
                    else if (!instruction.OriginalChecksums.SequenceEqual(originalPathsToChecksum))
                    {
                        Logger.Log("Warning! Original checksums of your KOTOR directory do not match the instructions file.");
                    }*/

                        //parse the source/destinations

                        bool success = false;
                        switch (instruction.Action.ToLower())
                        {
                            case "extract":
                                success = await instruction.ExtractFileAsync();
                                break;
                            case "delete":
                                success = instruction.DeleteFile();
                                break;
                            case "move":
                                success = instruction.MoveFile();
                                break;
                            case "patch":
                            case "tslpatcher":
                                //success = await instruction.ExecuteTSLPatcherAsync();
                                success = await instruction.ExecuteProgramAsync();
                                //success = success && await instruction.VerifyInstallAsync();
                                break;
                            case "execute":
                            case "run":
                                success = await instruction.ExecuteProgramAsync();
                                break;
                            case "backup": //todo
                            case "rename": //todo
                            case "confirm":
                            /*(var sourcePaths, var something) = instruction.ParsePaths();
                        bool confirmationResult = await confirmDialog.ShowConfirmationDialog(sourcePaths.FirstOrDefault());
                        if (!confirmationResult)
                        {
                            this.Confirmations.Add(true);
                        }
                        break;*/
                            case "inform":
                            case "choose": //todo, will rely on future instructions in the list.
                            default:
                                // Handle unknown instruction type here
                                Logger.Log($"Unknown instruction {instruction.Action}");
                                success = false;
                                break;
                        }

                        if (success)
                        {
                            Logger.Log($"Successfully completed instruction #{i1} '{instruction.Action}'");
                            continue;
                        }

                        Logger.LogException(new InvalidOperationException($"Instruction {instruction.Action} failed at index {i1}."));
                        bool confirmationResult = await confirmDialog.ShowConfirmationDialog($"Error installing mod {Name}, would you like to execute the next instruction anyway?");
                        if (confirmationResult)
                            continue;

                        return (false, null);

                        /*if (instruction.ExpectedChecksums != null)
                    {
                        // Get the new checksums after the modifications
                        var validator = new FileChecksumValidator(
                            destinationPath: MainConfig.DestinationPath.FullName,
                            expectedChecksums: instruction.ExpectedChecksums,
                            originalChecksums: originalPathsToChecksum
                        );
                        bool checksumsMatch = await validator.ValidateChecksumsAsync();
                        if (checksumsMatch)
                        {
                            Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' succeeded and modified files have expected checksums.");
                        }
                        else
                        {
                            Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' succeeded but modified files have unexpected checksums.");
                            bool confirmationResult = await confirmDialog.ShowConfirmationDialog("Warning! Checksums after running install step are not the same as expected. Continue anyway?");
                            if (!confirmationResult)
                            {
                                return (false, originalPathsToChecksum);
                            }
                        }
                    }
                    else
                    {
                        Logger.Log($"Component {component.Name}'s instruction '{instruction.Action}' ran, saving the new checksums as expected.");
                        var newChecksums = new Dictionary<FileInfo, System.Security.Cryptography.SHA1>();
                        foreach (FileInfo file in MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories))
                        {
                            System.Security.Cryptography.SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(file);
                            newChecksums[file] = sha1;
                        }
                    }*/
                    }

                    return (true, new Dictionary<FileInfo, SHA1>());
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogException(ex);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                Logger.Log("The above exception is not planned and has not been experienced - please report this to the developer.");
            }

            return (false, new Dictionary<FileInfo, SHA1>());
        }
    }
}
