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
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Nett;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using Tomlyn.Model;
using static KOTORModSync.Core.Component;
using Component = KOTORModSync.Core.Component;
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
        public Dictionary<Guid, Option> Options { get; set; }
        public List<string> Language { get; private set; }
        public string ModLink { get; set; }
        public bool IsSelected { get; set; }
        public List<Option> ChosenOptions { get; set; }
        private ComponentValidation Validator { get; set; }

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
            var rootTable = new Dictionary<string, List<object>>
            {
                ["thisMod"] = new List<object>( 65535 ) { Serializer.SerializeObject( this ) }
            };

            // Loop through the "thisMod" list
            for ( int i = 0;
                i < rootTable["thisMod"].Count;
                i++ )
            {
                // Check if the item is a Dictionary<string, object> representing a TOML table
                if ( !( rootTable["thisMod"][i] is IDictionary<string, object> table
                        && table.TryGetValue( "Instructions", out object value ) ) )
                {
                    continue;
                }

                // Check if the "Instructions" table is empty
                // Remove the empty "Instructions" table from the root table
                if ( value is IList<object> instructions && instructions.Count != 0 )
                    continue;
                _ = table.Remove( "Instructions" );

                // Check if the "Options" table is empty
                // Remove the empty "Options" table from the root table
                if ( value is List<object> options && options.Count != 0 )
                    continue;
                _ = table.Remove( "Options" );

                // don't serialize stuff chosen during an install.
                _ = table.Remove( "ChosenOptions" );

                break;
            }

            string tomlString = Toml.WriteString( rootTable );
            return Serializer.FixWhitespaceIssues( tomlString );
        }

        private DirectoryInfo _tempPath;

        public void DeserializeComponent( TomlObject tomlObject )
        {
            if ( !( tomlObject is TomlTable componentTable ) )
            {
                throw new ArgumentException(
                    "[TomlError] Expected a TOML table for component data." );
            }

            _tempPath = new DirectoryInfo( Path.GetTempPath() );

            Dictionary<string, object> componentDict
                = Serializer.ConvertTomlTableToDictionary( componentTable );

            Serializer.DeserializePath( componentDict, "paths" );
            this.Name = GetRequiredValue<string>( componentDict, "name" );
            Logger.Log( $"\r\n== Deserialize next component '{this.Name}' ==" );
            this.Guid = GetRequiredValue<Guid>( componentDict, "guid" );
            this.Description = GetValueOrDefault<string>( componentDict, "description" );
            this.Directions = GetValueOrDefault<string>( componentDict, "directions" );
            this.Category = GetValueOrDefault<string>( componentDict, "category" );
            this.Tier = GetValueOrDefault<string>( componentDict, "tier" );
            this.Language = GetValueOrDefault<List<string>>( componentDict, "language" );
            this.Author = GetValueOrDefault<string>( componentDict, "author" );
            this.Dependencies = GetValueOrDefault<List<string>>( componentDict, "dependencies" );
            this.Restrictions = GetValueOrDefault<List<string>>( componentDict, "restrictions" );
            this.ModLink = GetValueOrDefault<string>( componentDict, "modlink" );

            this.Instructions = DeserializeInstructions(
                GetValueOrDefault<TomlTableArray>( componentDict, "instructions" ) );
            this.Instructions.ForEach( instruction => instruction.SetParentComponent( this ) );

            this.Options = DeserializeOptions(
                GetValueOrDefault<TomlTableArray>( componentDict, "options" )
            );

            // can't validate anything if directories aren't set.
            if ( string.IsNullOrWhiteSpace( MainConfig.SourcePath?.FullName )
                || string.IsNullOrWhiteSpace( MainConfig.DestinationPath?.FullName ) )
            {
                return;
            }

            // Validate and log additional errors/warnings.
            this.Validator = new ComponentValidation( this );
            if ( this.Validator.Run() )
            {
                Logger.Log( $"Successfully deserialized component '{this.Name}'\r\n" );
                return;
            }

            this.Validator.GetErrors().ForEach( Logger.LogError );
            this.Validator.GetWarnings().ForEach( Logger.LogWarning );
        }

        [NotNull]
        private Dictionary<Guid, Option> DeserializeOptions( [CanBeNull] TomlTableArray tomlObject )
        {
            if ( tomlObject == null )
            {
                Logger.LogVerbose( $"No options found for component '{this.Name}'" );
                return new Dictionary<Guid, Option>();
            }

            var options = new Dictionary<Guid, Option>( 65535 );

            foreach ( TomlTable item in tomlObject )
            {
                Dictionary<string, object> optionDict = Serializer.ConvertTomlTableToDictionary( item );

                // ConvertTomlTableToDictionary lowercase all string keys.
                Serializer.DeserializePath( optionDict, "source" );
                Serializer.DeserializeGuidDictionary( optionDict, "restrictions" );
                Serializer.DeserializeGuidDictionary( optionDict, "dependencies" );

                var option = new Option();
                option.Source = GetRequiredValue<List<string>>( optionDict, "source" );
                option.Guid = GetValueOrDefault<Guid>( optionDict, "guid" );
                option.Destination = GetRequiredValue<string>( optionDict, "destination" );
                option.Restrictions = GetValueOrDefault<List<string>>( optionDict, "restrictions" )
                    ?.Select( Guid.Parse )
                    .ToList();

                option.Dependencies = GetValueOrDefault<List<string>>( optionDict, "dependencies" )
                    ?.Select( Guid.Parse )
                    .ToList();

                options.Add( Guid.NewGuid(), option ); // Generate a new GUID key for each option
            }

            return options;
        }

        [NotNull]
        private List<Instruction> DeserializeInstructions( [CanBeNull] TomlTableArray tomlObject )
        {
            if ( tomlObject == null )
            {
                Logger.LogWarning( $"No instructions found for component '{this.Name}'" );
                return new List<Instruction>();
            }

            var instructions = new List<Instruction>( 65535 );

            for ( int index = 0;
                 index < tomlObject.Count;
                 index++ )
            {
                TomlTable item = tomlObject[index];
                Dictionary<string, object> instructionDict
                    = Serializer.ConvertTomlTableToDictionary( item );

                // ConvertTomlTableToDictionary lowercase all string keys.
                Serializer.DeserializePath( instructionDict, "source" );
                Serializer.DeserializeGuidDictionary( instructionDict, "restrictions" );
                Serializer.DeserializeGuidDictionary( instructionDict, "dependencies" );

                var instruction = new Instruction();
                instruction.Action = GetRequiredValue<string>( instructionDict, "action" );
                instruction.Guid = GetValueOrDefault<Guid>( instructionDict, "guid" );
                Logger.Log(
                    $"\r\n-- Deserialize instruction #{index + 1} action {instruction.Action}" );
                instruction.Arguments = GetValueOrDefault<string>( instructionDict, "arguments" );
                instruction.Overwrite = GetValueOrDefault<bool>( instructionDict, "overwrite" );

                instruction.Restrictions = GetValueOrDefault<List<string>>( instructionDict, "restrictions" )
                    ?.Where( restriction => restriction != null )
                    .Select(
                        restriction =>
                        {
                            if ( Guid.TryParse( restriction, out Guid parsedGuid ) )
                                return parsedGuid;

                            return Guid.Empty; // or any other default value for invalid GUIDs
                        } )
                    .ToList();

                instruction.Dependencies = GetValueOrDefault<List<string>>( instructionDict, "dependencies" )
                    ?.Where( dependency => dependency != null )
                    .Select(
                        dependency =>
                        {
                            if ( Guid.TryParse( dependency, out Guid parsedGuid ) )
                                return parsedGuid;

                            return Guid.Empty; // or any other default value for invalid GUIDs
                        } )
                    .ToList();

                instruction.Source = GetValueOrDefault<List<string>>( instructionDict, "source" );
                instruction.Destination = GetValueOrDefault<string>( instructionDict, "destination" );
                instructions.Add( instruction );
            }

            return instructions;
        }

        [CanBeNull]
        private static T GetRequiredValue
            <T>( IReadOnlyDictionary<string, object> dict, string key ) =>
            GetValue<T>( dict, key, true );

        [CanBeNull]
        private static T GetValueOrDefault
            <T>( IReadOnlyDictionary<string, object> dict, string key ) =>
            GetValue<T>( dict, key, false );

        private static T GetValue<T>(
            IReadOnlyDictionary<string, object> dict,
            string key,
            bool required
        )
        {
            if ( !dict.TryGetValue( key, out object value ) )
            {
                string caseInsensitiveKey = dict.Keys.FirstOrDefault(
                    k => k.Equals( key, StringComparison.OrdinalIgnoreCase ) );

                if ( caseInsensitiveKey == null )
                {
                    if ( !required )
                        return default;

                    throw new ArgumentException( $"[Error] Missing or invalid '{key}' field." );
                }

                value = dict[caseInsensitiveKey];
            }

            if ( value is T t )
                return t;

            Type targetType = value.GetType();

            if ( value is string valueStr
                 && typeof( T ) == typeof( Guid )
                 && Guid.TryParse( valueStr, out Guid guid )
               )
            {
                return (T)(object)guid;
            }

            if ( targetType.IsGenericType
                 && targetType.GetGenericTypeDefinition() == typeof( List<> )
                 && value is IEnumerable enumerable
               )
            {
                Type elementType = typeof( T ).GetGenericArguments()[0];
                dynamic dynamicList
                    = Activator.CreateInstance( typeof( List<> ).MakeGenericType( elementType ) );

                foreach ( object item in enumerable )
                {
                    dynamic convertedItem = Convert.ChangeType( item, elementType );
                    dynamicList.Add( convertedItem );
                }

                return dynamicList;
            }

            if ( value is string valueStr2 && string.IsNullOrEmpty( valueStr2 ) )
            {
                return required ? throw new ArgumentException( $"'{key}' field is null or empty." )
                    : (T)default;
            }

            if ( typeof( T ).IsGenericType && typeof( T ).GetGenericTypeDefinition() == typeof( List<> ) )
            {
                Type listType = typeof( T );
                Type elementType = listType.GetGenericArguments()[0];
                dynamic dynamicList = Activator.CreateInstance( listType );

                foreach ( object item in (IEnumerable)value )
                {
                    dynamic convertedItem = Convert.ChangeType( item, elementType );
                    dynamicList.Add( convertedItem );
                }

                return dynamicList;
            }

            if ( targetType == typeof( Tomlyn.Model.TomlArray )
                 && value is Tomlyn.Model.TomlArray valueTomlArray )
            {
                if ( valueTomlArray.Count == 0 ) return default;

                var tomlTableArray = new TomlTableArray();
                foreach ( object tomlValue in valueTomlArray )
                {
                    if ( tomlValue is TomlTable table )
                        tomlTableArray.Add( table );
                }

                return (T)(object)tomlTableArray;
            }

            try
            {
                return (T)Convert.ChangeType( value, typeof( T ) );
            }
            catch ( InvalidCastException )
            {
                if ( required )
                    throw new ArgumentException( $"Invalid '{key}' field type." );
            }
            catch ( FormatException )
            {
                if ( required )
                    throw new ArgumentException( $"Invalid format for '{key}' field." );
            }

            return default;
        }

        public enum ExecutionResult
        {
            [Description( "Success" )]
            Success,

            [Description( "Directory permission denied." )]
            DirectoryPermissionDenied,

            [Description( "Component installation failed." )]
            ComponentInstallationFailed
        }

        public async Task<(bool success, Dictionary<FileInfo, SHA1> originalChecksums)>
            ExecuteInstructions(
                Utility.Utility.IConfirmationDialogCallback confirmDialog,
                Utility.Utility.IOptionsDialogCallback optionsDialog,
                List<Component> componentsList
            )
        {
            try
            {
                // Check if we have permission to write to the Destination directory
                if ( !Utility.Utility.IsDirectoryWritable( MainConfig.DestinationPath ) )
                {
                    throw new InvalidOperationException(
                        "[Error] Cannot write to the destination directory." );
                }

                (bool, Dictionary<FileInfo, SHA1>) result = await ProcessComponentAsync( this );
                if ( result.Item1 )
                    return (true, null);

                await Logger.LogExceptionAsync(
                    new Exception(
                        $"[Error] Component {Name} failed to install the mod correctly with {result}" )
                );
                return (false, null);

                async Task<(bool, Dictionary<FileInfo, SHA1>)> ProcessComponentAsync(
                    Component component
                )
                {
                    for ( int i1 = 0;
                         i1 < component.Instructions.Count;
                         i1++ )
                    {
                        Instruction instruction = component.Instructions[i1]
                            ?? throw new ArgumentException(
                                $"[Error] instruction null at index {i1}",
                                nameof( componentsList )
                            );

                        //The instruction will run if any of the following conditions are met:
                        //The instruction has no dependencies or restrictions.
                        //The instruction has dependencies, and all of the required components are being installed.
                        //The instruction has restrictions, but none of the restricted components are being installed.
                        bool shouldRunInstruction = true;
                        if ( instruction.Dependencies?.Count > 0 )
                        {
                            shouldRunInstruction = instruction.Dependencies.All(
                                requiredGuid => componentsList.Any(
                                    checkComponent => checkComponent.Guid == requiredGuid
                                )
                            );
                            if ( !shouldRunInstruction )
                            {
                                await Logger.LogAsync(
                                    $"[Information] Skipping instruction '{instruction.Action}' index {i1} due to missing dependency(s): {instruction.Dependencies}"
                                );
                            }
                        }

                        if ( instruction.Restrictions?.Count > 0 )
                        {
                            shouldRunInstruction &= !instruction.Restrictions.Any(
                                restrictedGuid => componentsList.Any(
                                    checkComponent => checkComponent.Guid == restrictedGuid
                                )
                            );
                            if ( !shouldRunInstruction )
                            {
                                await Logger.LogAsync(
                                    $"[Information] Not running instruction {instruction.Action} index {i1} due to restricted components installed: {instruction.Restrictions}"
                                );
                            }
                        }

                        if ( !shouldRunInstruction ) continue;

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

                        // converts the custom variables and enumerates the files/folders based on wildcards
                        instruction.SetRealPaths();

                        switch ( instruction.Action.ToLower() )
                        {
                            case "extract":
                                success = await instruction.ExtractFileAsync();
                                break;
                            case "delete":
                                success = instruction.DeleteFile();
                                break;
                            case "delduplicate":
                                Instruction.DeleteDuplicateFile(
                                    instruction.Destination,
                                    instruction.Arguments
                                );
                                break;
                            case "copy":
                                success = instruction.CopyFile();
                                break;
                            case "move":
                                success = instruction.MoveFile();
                                break;
                            case "rename":
                                success = instruction.RenameFile();
                                break;
                            case "patch":
                            case "tslpatcher":
                                success = MainConfig.TslPatcherCli
                                    ? await instruction.ExecuteTSLPatcherAsync()
                                    : await instruction.ExecuteProgramAsync();

                                try
                                {
                                    List<string> installErrors = instruction.VerifyInstall();
                                    if ( installErrors.Count > 0 )
                                        await Logger.LogAsync( string.Join( "\n", installErrors ) );

                                    success &= installErrors.Count == 0;
                                }
                                catch ( FileNotFoundException )
                                {
                                    success = false;
                                }

                                break;
                            case "execute":
                            case "run":
                                success = await instruction.ExecuteProgramAsync();
                                break;
                            case "backup": //todo
                            case "confirm":
                            /*(var sourcePaths, var something) = instruction.ParsePaths();
                        bool confirmationResult = await confirmDialog.ShowConfirmationDialog(sourcePaths.FirstOrDefault());
                        if (!confirmationResult)
                        {
                            this.Confirmations.Add(true);
                        }
                        break;*/
                            case "inform":
                            case "choose":
                                List<Option> options = this.ChooseOptions( instruction );
                                List<string> optionNames = options.ConvertAll( option => option.Name );

                                string selectedOptionName = await optionsDialog.ShowOptionsDialog( optionNames )
                                    ?? throw new NullReferenceException( nameof( optionNames ) );
                                Option selectedOption = null;

                                foreach ( Option option in options )
                                {
                                    string optionName = option.Name;
                                    if ( optionName != selectedOptionName )
                                        continue;

                                    selectedOption = option;
                                    break;
                                }

                                if ( selectedOption != null )
                                    this.ChosenOptions.Add( selectedOption );

                                break;
                            default:
                                // Handle unknown instruction type here
                                await Logger.LogWarningAsync( $"Unknown instruction {instruction.Action}" );
                                success = false;
                                break;
                        }

                        if ( !success )
                        {
                            await Logger.LogAsync( $"Instruction {instruction.Action} failed at index {i1}." );
                            bool? confirmationResult = await confirmDialog.ShowConfirmationDialog(
                                $"An error occurred during the installation of mod '{this.Name}'."
                                + " Do you want to retry this instruction?" + Environment.NewLine + Environment.NewLine
                                + " Select 'Yes' to retry,"
                                + " 'No' to proceed to the next instruction,"
                                + $" or close this window to abort the installation of '{this.Name}'."
                            );



                            switch ( confirmationResult )
                            {
                                // repeat instruction
                                case true:
                                    i1--;
                                    continue;

                                // execute next instruction
                                case false:
                                    continue;

                                // case null: cancel installing this mod (user closed confirmation dialog)
                                default:
                                    return (false, null);
                            }
                        }

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

                        await Logger.LogAsync( $"Successfully completed instruction #{i1 + 1} '{instruction.Action}'" );
                    }

                    return (true, new Dictionary<FileInfo, SHA1>());
                }
            }
            catch ( InvalidOperationException ex ) { await Logger.LogExceptionAsync( ex ); }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                await Logger.LogAsync(
                    "The above exception is not planned and has not been experienced."
                    + " Please report this to the developer."
                );
            }

            return (false, new Dictionary<FileInfo, SHA1>());
        }

        private List<Option> ChooseOptions( Instruction instruction )
        {
            if ( instruction is null )
                throw new ArgumentNullException( nameof( instruction ) );

            List<string> archives = Validator.GetAllArchivesFromInstructions( component: this );

            if ( archives.Count == 0 )
                throw new InvalidOperationException( "No archives found." );

            return Options.Values.Where(
                option =>
                    option.Source.Any(
                        sourcePath =>
                            archives.Contains( sourcePath )
                    )
            ).ToList();
        }
    }

    public class ValidationResult
    {
        public int InstructionIndex { get; }
        public Instruction Instruction { get; }
        public Component Component { get; }
        public string Message { get; }
        public bool IsError { get; }

        public ValidationResult(
            ComponentValidation validator,
            Instruction instruction,
            string message,
            bool isError
        )
        {
            Component = validator.Component;
            Instruction = instruction;
            InstructionIndex = Component.Instructions.IndexOf( instruction );
            Message = message;
            IsError = isError;

            Logger.Log(
                $"{( IsError ? "[Error]" : "[Warning]" )}"
                + $" Component: '{Component.Name}',"
                + $" Instruction #{InstructionIndex + 1},"
                + $" Action '{instruction.Action}'"
            );
            Logger.Log( $"{( IsError ? "[Error]" : "[Warning]" )} {Message}" );
        }
    }

    public class ComponentValidation
    {
        public readonly Component Component;
        private readonly List<ValidationResult> _validationResults;

        public ComponentValidation( Component component )
        {
            Component = component;
            _validationResults = new List<ValidationResult>();
        }

        private void AddError( string message, Instruction instruction )
        {
            _validationResults.Add(
                new ValidationResult(
                    this,
                    instruction,
                    message,
                    isError: true ) );
        }

        private void AddWarning( string message, Instruction instruction )
        {
            _validationResults.Add(
                new ValidationResult(
                    this,
                    instruction,
                    message,
                    isError: false ) );
        }

        public List<string> GetErrors()
        {
            return _validationResults
                .Where( r => r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public List<string> GetErrors( int instructionIndex )
        {
            return _validationResults
                .Where( r => r.InstructionIndex == instructionIndex && r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public List<string> GetErrors( Instruction instruction )
        {
            return _validationResults
                .Where( r => r.Instruction == instruction && r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public List<string> GetWarnings()
        {
            return _validationResults
                .Where( r => !r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public List<string> GetWarnings( int instructionIndex )
        {
            return _validationResults
                .Where( r => r.InstructionIndex == instructionIndex && !r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public List<string> GetWarnings( Instruction instruction )
        {
            return _validationResults
                .Where( r => r.Instruction == instruction && !r.IsError )
                .Select( r => r.Message )
                .ToList();
        }

        public bool VerifyExtractPaths( Component component )
        {
            try
            {
                List<string> allArchives = GetAllArchivesFromInstructions( component );
                if ( allArchives.Count == 0 ) return true;

                bool success = true;
                foreach ( Instruction instruction in component.Instructions )
                {
                    // we already checked if the archive exists in GetAllArchivesFromInstructions.
                    if ( instruction.Action.Equals( "extract", StringComparison.OrdinalIgnoreCase ) )
                        continue;

                    bool archiveNameFound = true;
                    if ( instruction.Source == null )
                    {
                        this.AddWarning(
                            "Instruction does not have a 'Source' key defined",
                            instruction
                        );
                        success = false;
                        continue;
                    }

                    if ( instruction.Source == null )
                    {
                        this.AddError( "Missing 'Source' key", instruction );
                        continue;
                    }

                    foreach ( string sourcePath in instruction.Source )
                    {
                        if ( sourcePath.StartsWith( "<<kotorDirectory>>" ) )
                            continue;

                        (bool, bool) result = IsSourcePathInArchives( sourcePath, allArchives, instruction );
                        success &= result.Item1;
                        archiveNameFound &= result.Item2;
                    }

                    if ( !archiveNameFound )
                    {
                        AddWarning(
                            "'Source' path does not include the archive's name as part"
                            + " of the extraction folder, possible FileNotFound exception.",
                            instruction
                        );
                    }
                }

                return success;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
                return false;
            }
        }

        public List<string> GetAllArchivesFromInstructions( Component component )
        {
            var allArchives = new List<string>();

            foreach ( Instruction instruction in component.Instructions )
            {
                if ( instruction.Source == null || instruction.Action != "extract" )
                    continue;

                List<string> realPaths = FileHelper.EnumerateFilesWithWildcards(
                    instruction.Source.ConvertAll( Utility.Utility.ReplaceCustomVariables ),
                    true
                );
                foreach ( string realSourcePath in realPaths )
                {
                    if ( !ArchiveHelper.IsArchive( Path.GetExtension( realSourcePath ) ) )
                    {
                        AddWarning(
                            $"Archive '{Path.GetFileName( realSourcePath )}'"
                            + $" is referenced in a non 'extract' action. Was this intentional?",
                            instruction
                        );
                        continue;
                    }

                    if ( !File.Exists( realSourcePath ) )
                    {
                        AddError(
                            $"Missing required download:"
                            + $" '{Path.GetFileNameWithoutExtension( realSourcePath )}'",
                            instruction
                        );
                        continue;
                    }

                    allArchives.Add( realSourcePath );
                }
            }

            return allArchives;
        }

        public bool ParseDestinationWithAction( Component component )
        {
            bool success = true;
            foreach ( Instruction instruction in component.Instructions )
            {
                DirectoryInfo destinationPath = null;
                if ( !string.IsNullOrWhiteSpace(instruction.Destination) )
                {
                    destinationPath = new DirectoryInfo(
                        Utility.Utility.ReplaceCustomVariables( instruction.Destination )
                    );
                }

                switch ( instruction.Action )
                {
                    case null:
                        continue;
                    // tslpatcher must always use <<kotorDirectory>> and nothing else.
                    case "tslpatcher" when instruction.Destination == null:
                        instruction.Destination = "<<kotorDirectory>>";
                        break;

                    case "tslpatcher" when ( !instruction.Destination.Equals( "<<kotorDirectory>>" ) ):
                        success = false;
                        AddError(
                            $"'Destination' key must be either null or string literal '<<kotorDirectory>>'"
                            + $" for this action. Got '{instruction.Destination}'",
                            instruction
                        );
                        if ( MainConfig.AttemptFixes )
                        {
                            Logger.Log( "Fixing the above issue automatically." );
                            instruction.Destination = "<<kotorDirectory>>";
                        }

                        break;
                    // extract and delete cannot use the 'Destination' key.
                    case "extract":
                    case "delete":
                        if ( instruction.Destination == null )
                            break;

                        success = false;
                        AddError(
                            $"'Destination' key cannot be used with this action."
                            + $" Got '{instruction.Destination}'",
                            instruction
                        );

                        if ( !MainConfig.AttemptFixes )
                            break;

                        Logger.Log( "Fixing the above issue automatically." );
                        instruction.Destination = null;

                        break;
                    // rename should never use <<kotorDirectory >>\\Override
                    case "rename":
                        if ( instruction.Destination?.Equals(
                                "<<kotorDirectory>>\\Override",
                                StringComparison.Ordinal ) != false
                           )
                        {
                            success = false;
                            AddError(
                                $"Incorrect 'Destination' format."
                                + $" Got '{instruction.Destination}',"
                                + $" expected a filename.",
                                instruction
                            );
                        }

                        break;
                    default:
                        if ( destinationPath?.Exists != true )
                        {
                            success = false;
                            AddError(
                                $"Destination cannot be found!"
                                + $" Got '{destinationPath?.FullName}'",
                                instruction
                            );

                            if ( !MainConfig.AttemptFixes )
                                break;

                            Logger.Log(
                                "Fixing the above issue automatically"
                                + " (setting Destination to '<<kotorDirectory>>\\Override')" );
                            instruction.Destination = "<<kotorDirectory>>\\Override";
                        }

                        break;
                }
            }

            return success;
        }

        private static string GetErrorDescription( ArchivePathCode code )
        {
            switch ( code )
            {
                case ArchivePathCode.FoundSuccessfully:
                    return "File successfully found in archive.";
                case ArchivePathCode.NotAnArchive:
                    return "Not an archive";
                case ArchivePathCode.PathMissingArchiveName:
                    return "Missing archive name in path";
                case ArchivePathCode.CouldNotOpenArchive:
                    return "Could not open archive";
                case ArchivePathCode.NotFoundInArchive:
                    return "Not found in archive";
                case ArchivePathCode.NoArchivesFound:
                    return "No archives found/no extract instructions created";
                default:
                    return "Unknown error";
            }
        }

        public enum ArchivePathCode
        {
            NotAnArchive,
            PathMissingArchiveName,
            CouldNotOpenArchive,
            NotFoundInArchive,
            FoundSuccessfully,
            NoArchivesFound
        }

        public (bool, bool) IsSourcePathInArchives(
            string sourcePath,
            List<string> allArchives,
            Instruction instruction
        )
        {
            bool foundInAnyArchive = false;
            bool hasError = false;
            bool archiveNameFound = false;
            string errorDescription = string.Empty;

            sourcePath = sourcePath
                .Replace( '/', '\\' )
                .Replace( "<<modDirectory>>\\", "" )
                .Replace( "<<kotorDirectory>>\\", "" );

            foreach ( string archivePath in allArchives )
            {
                // Check if the archive name matches the first portion of the sourcePath
                string archiveName = Path.GetFileNameWithoutExtension( archivePath );
                string[] pathParts = sourcePath.TrimEnd( '\\' ).Split( '\\' );
                archiveNameFound = FileHelper.WildcardMatch( archiveName, pathParts[0] );

                // Remove the trailing backslash from sourcePath if it exists
                sourcePath = sourcePath.TrimEnd( '\\' );

                ArchivePathCode code = IsPathInArchive( sourcePath, archivePath );

                if ( code == ArchivePathCode.FoundSuccessfully )
                {
                    foundInAnyArchive = true;
                    break;
                }

                if ( code == ArchivePathCode.NotFoundInArchive )
                    continue;

                hasError = true;
                errorDescription += GetErrorDescription( code ) + Environment.NewLine;
            }

            if ( hasError )
            {
                AddError(
                    $"Invalid source path '{sourcePath}'. Reason: {errorDescription}",
                    instruction
                );
                return (false, archiveNameFound);
            }

            if ( !foundInAnyArchive )
            {
                AddError( $"Failed to find '{sourcePath}' in any archives!", instruction );
                return (false, archiveNameFound);
            }

            return (true, true);
        }

        private static ArchivePathCode IsPathInArchive( string relativePath, string archivePath )
        {
            if ( !ArchiveHelper.IsArchive( Path.GetExtension( archivePath ) ) )
            {
                return ArchivePathCode.NotAnArchive;
            }

            using ( FileStream stream = File.OpenRead( archivePath ) )
            {
                IArchive archive = ArchiveHelper.OpenArchive( stream, archivePath );

                if ( archive == null )
                    return ArchivePathCode.CouldNotOpenArchive;

                // everything is extracted to a new directory named after the archive.
                string archiveNameAppend = Path.GetFileNameWithoutExtension( archivePath ) + "\\";

                // if the Source key represents the top level extraction directory, check that first.
                if ( FileHelper.WildcardMatch( archiveNameAppend, relativePath ) )
                    return ArchivePathCode.FoundSuccessfully;

                var folderPaths = new HashSet<string>();

                foreach ( IArchiveEntry entry in archive.Entries )
                {
                    // Append extracted directory and ensure every slash is a backslash.
                    string itemInArchivePath = archiveNameAppend + entry.Key.Replace( '/', '\\' );

                    // Some archives loop through folders while others don't.
                    // Check if itemInArchivePath has an extension to determine folderName.
                    string folderName = FileHelper.GetFolderName( itemInArchivePath );

                    // Add the folder path to the list, after removing trailing slashes.
                    if ( !string.IsNullOrEmpty( folderName ) )
                    {
                        _ = folderPaths.Add( folderName.TrimEnd( '\\', '/' ) );
                    }

                    // Check if itemInArchivePath matches relativePath using wildcard matching.
                    if ( FileHelper.WildcardMatch( itemInArchivePath, relativePath ) )
                    {
                        return ArchivePathCode.FoundSuccessfully;
                    }
                }

                // check if instruction.Source matches a folder.
                foreach ( string folderPath in folderPaths )
                {
                    if ( FileHelper.WildcardMatch( folderPath, relativePath ) )
                        return ArchivePathCode.FoundSuccessfully;
                }

                // quickly parse the path so the verbose log below is accurate.
                string result = relativePath;

                bool hasMultipleBackslashes = result.Count( c => c == '\\' ) > 1;
                bool endsWithBackslash = result.EndsWith( "\\", StringComparison.Ordinal );

                if ( hasMultipleBackslashes || endsWithBackslash )
                {
                    result = result.Substring( result.IndexOf( '\\' ) + 1 );
                }

                // not necessarily an error as there could be other archives to loop through.
                Logger.LogVerbose( $"'{result}' not found in '{Path.GetFileName( archivePath )}'" );
            }

            return ArchivePathCode.NotFoundInArchive;
        }

        public bool Run() =>
            // Verify all the instructions' paths line up with hierarchy of the archives
            this.VerifyExtractPaths( Component )
                   // Ensure all the 'Destination' keys are valid for their respective action.
                   && this.ParseDestinationWithAction( Component );
    }
}
