// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
using Nett;
using Tomlyn;
using Tomlyn.Syntax;
using static KOTORModSync.Core.Utility.CallbackObjects;
using Toml = Tomlyn.Toml;
using TomlObject = Tomlyn.Model.TomlObject;
using TomlTable = Tomlyn.Model.TomlTable;
using TomlTableArray = Tomlyn.Model.TomlTableArray;

namespace KOTORModSync.Core
{
    public class Component : INotifyPropertyChanged
    {
        public enum ExecutionResult
        {
            [Description( "Success" )] Success,

            [Description( "Directory permission denied." )]
            DirectoryPermissionDenied,

            [Description( "Component installation failed." )]
            ComponentInstallationFailed
        }

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

        [NotNull] private DirectoryInfo _tempPath;
        [NotNull] public string Name { get; set; } = string.Empty;
        public Guid Guid { get; set; }
        [NotNull] public string Author { get; set; } = string.Empty;
        [NotNull] public string Category { get; set; } = string.Empty;
        [NotNull] public string Tier { get; set; } = string.Empty;
        [NotNull] public string Description { get; set; } = string.Empty;
        [NotNull] public string Directions { get; set; } = string.Empty;
        [NotNull] private List<Guid> _dependencies = new List<Guid>();

        [NotNull]
        public List<Guid> Dependencies
        {
            get => _dependencies;
            set
            {
                _dependencies = value;
                OnPropertyChanged();
            }
        }


        [NotNull] private List<Guid> _restrictions = new List<Guid>();

        [NotNull]
        public List<Guid> Restrictions
        {
            get => _restrictions;
            set
            {
                _restrictions = value;
                OnPropertyChanged();
            }
        }


        [NotNull] public List<Guid> InstallBefore { get; set; } = new List<Guid>();
        [NotNull] public List<Guid> InstallAfter { get; set; } = new List<Guid>();
        public bool NonEnglishFunctionality { get; set; }
        [NotNull] public string InstallationMethod { get; set; } = string.Empty;
        [NotNull] public List<Instruction> Instructions { get; set; } = new List<Instruction>();
        [NotNull] public Dictionary<Guid, Option> Options { get; set; } = new Dictionary<Guid, Option>();
        [NotNull] public List<string> Language { get; private set; } = new List<string>();
        [NotNull] public string ModLink { get; set; } = string.Empty;
        [NotNull] public List<Option> ChosenOptions { get; set; } = new List<Option>();
        private ComponentValidation Validator { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if ( _isSelected == value )
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        [NotNull]
        public string SerializeComponent()
        {
            _ = TomlSettings.Create();
            var rootTable = new Dictionary<string, List<object>>
            {
                ["thisMod"] = new List<object>( 65535 ) { Serializer.SerializeObject( this ) }
            };

            // Loop through the "thisMod" list
            for ( int i = 0; i < rootTable["thisMod"].Count; i++ )
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
                {
                    continue;
                }

                _ = table.Remove( "Instructions" );

                // Check if the "Options" table is empty
                // Remove the empty "Options" table from the root table
                if ( value is List<object> options && options.Count != 0 )
                {
                    continue;
                }

                _ = table.Remove( "Options" );

                // don't serialize stuff chosen during an install.
                _ = table.Remove( "ChosenOptions" );

                break;
            }

            string tomlString = Nett.Toml.WriteString( rootTable );
            return Serializer.FixWhitespaceIssues( tomlString ?? string.Empty );
        }

        public void DeserializeComponent( [NotNull] TomlObject tomlObject )
        {
            if ( !( tomlObject is TomlTable componentTable ) ) throw new ArgumentException( "[TomlError] Expected a TOML table for component data." );

            _tempPath = new DirectoryInfo( Path.GetTempPath() );

            Dictionary<string, object> componentDict = Serializer.ConvertTomlTableToDictionary( componentTable );

            // reminder: ConvertTomlTableToDictionary lowercases all string keys automatically.
            Name = GetRequiredValue<string>( componentDict, "name" );
            _ = Logger.LogAsync( $"{Environment.NewLine}== Deserialize next component '{Name}' ==" );
            Guid = GetRequiredValue<Guid>( componentDict, "guid" );
            Description = GetValueOrDefault<string>( componentDict, "description" );
            Directions = GetValueOrDefault<string>( componentDict, "directions" );
            Category = GetValueOrDefault<string>( componentDict, "category" );
            Tier = GetValueOrDefault<string>( componentDict, "tier" );
            Language = GetValueOrDefault<List<string>>( componentDict, "language" );
            Author = GetValueOrDefault<string>( componentDict, "author" );
            Dependencies = new List<Guid>( GetValueOrDefault<List<Guid>>( componentDict, "dependencies" ) );
            Restrictions = GetValueOrDefault<List<Guid>>( componentDict, "restrictions" );
            InstallBefore = GetValueOrDefault<List<Guid>>( componentDict, "installbefore" );
            InstallAfter = GetValueOrDefault<List<Guid>>( componentDict, "installafter" );
            ModLink = GetValueOrDefault<string>( componentDict, "modlink" );
            IsSelected = GetValueOrDefault<bool>( componentDict, "isselected" );

            Instructions = DeserializeInstructions(
                GetValueOrDefault<TomlTableArray>( componentDict, "instructions" )
            );
            Instructions.ForEach( instruction => instruction.SetParentComponent( this ) );

            Options = DeserializeOptions( GetValueOrDefault<TomlTableArray>( componentDict, "options" ) );

            // can't validate anything if directories aren't set.
            if ( string.IsNullOrWhiteSpace( MainConfig.SourcePath?.FullName )
                || string.IsNullOrWhiteSpace( MainConfig.DestinationPath?.FullName ) )
            {
                return;
            }

            // Validate and log additional errors/warnings.
            _ = Logger.LogAsync( $"Successfully deserialized component '{Name}'" );
        }

        public static void OutputConfigFile(
            [ItemNotNull][NotNull] IEnumerable<Component> components,
            [NotNull] string filePath
        )
        {
            if ( components is null ) throw new ArgumentNullException( nameof( components ) );
            if ( filePath is null ) throw new ArgumentNullException( nameof( filePath ) );

            var stringBuilder = new StringBuilder( 65535 );

            foreach ( Component thisComponent in components )
            {
                _ = stringBuilder.AppendLine( thisComponent.SerializeComponent() );
            }

            string tomlString = stringBuilder.ToString();
            File.WriteAllText( filePath, tomlString );
        }

        public static string GenerateModDocumentation( [NotNull] List<Component> componentsList )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            var sb = new StringBuilder( 50000 );
            const string indentation = "    ";

            // Loop through each 'thisMod' entry
            foreach ( Component component in componentsList )
            {
                _ = sb.AppendLine();

                // Component Information
                _ = sb.Append( "####**" ).Append( component.Name ).AppendLine( "**" );
                _ = sb.Append( "**Author**: " ).AppendLine( component.Author );
                _ = sb.AppendLine();
                _ = sb.Append( "**Description**: " ).AppendLine( component.Description );
                _ = sb.Append( "**Tier & Category**: " )
                    .Append( component.Tier )
                    .Append( " - " )
                    .AppendLine( component.Category );
                if ( component.Language != null )
                {
                    _ = string.Equals( component.Language.FirstOrDefault(), "All", StringComparison.OrdinalIgnoreCase )
                        ? sb.AppendLine( "**Supported Languages**: ALL" )
                        : sb.Append( "**Supported Languages**: [" )
                            .Append( Environment.NewLine )
                            .Append(
                                string.Join(
                                    $",{Environment.NewLine}",
                                    component.Language.Select( item => $"{indentation}{item}" )
                                )
                            )
                            .Append( Environment.NewLine )
                            .Append( ']' )
                            .AppendLine();
                }

                _ = sb.Append( "**Directions**: " ).AppendLine( component.Directions );

                // Instructions
                if ( component.Instructions is null ) continue;

                _ = sb.AppendLine();
                _ = sb.AppendLine( "**Installation Instructions:**" );
                foreach (
                    Instruction instruction
                    in component.Instructions
                        .Where(
                            instruction =>
                                !( instruction is null )
                                && instruction.Action != "extract"
                        )
                )
                {
                    _ = sb.Append( "**Action**: " ).AppendLine( instruction?.Action );
                    if ( instruction?.Action == "move" )
                    {
                        _ = sb.Append( "**Overwrite existing files?**: " )
                            .AppendLine(
                                instruction.Overwrite
                                    ? "NO"
                                    : "YES"
                            );
                    }

                    if ( instruction?.Source != null )
                    {
                        string thisLine
                            = $"Source: [{Environment.NewLine}{string.Join( $",{Environment.NewLine}", instruction.Source.Select( item => $"{indentation}{item}" ) )}{Environment.NewLine}]";

                        if ( instruction.Action != "move" )
                        {
                            thisLine = thisLine.Replace( "Source: ", "" );
                        }

                        _ = sb.AppendLine( thisLine );
                    }

                    if ( instruction?.Destination != null && instruction.Action == "move" )
                    {
                        _ = sb.Append( "Destination: " ).AppendLine( instruction.Destination );
                    }
                }
            }

            return sb.ToString();
        }

        [NotNull]
        private List<Instruction> DeserializeInstructions( [CanBeNull] TomlTableArray tomlObject )
        {
            if ( tomlObject is null )
            {
                _ = Logger.LogWarningAsync( $"No instructions found for component '{Name}'" );
                return new List<Instruction>();
            }

            var instructions = new List<Instruction>( 65535 );

            for ( int index = 0; index < tomlObject.Count; index++ )
            {
                TomlTable item = tomlObject[index];
                if ( item is null ) continue;

                Dictionary<string, object> instructionDict = Serializer.ConvertTomlTableToDictionary( item );

                // reminder: ConvertTomlTableToDictionary lowercases all string keys automatically.
                Serializer.DeserializePathInDictionary( instructionDict, "source" );
                Serializer.DeserializeGuidDictionary( instructionDict, "restrictions" );
                Serializer.DeserializeGuidDictionary( instructionDict, "dependencies" );

                var instruction = new Instruction { Action = GetRequiredValue<string>( instructionDict, "action" ) };
                _ = Logger.LogAsync(
                    $"{Environment.NewLine}-- Deserialize instruction #{index + 1} action {instruction.Action}"
                );
                instruction.Arguments = GetValueOrDefault<string>( instructionDict, "arguments" );
                instruction.Overwrite = GetValueOrDefault<bool>( instructionDict, "overwrite" );

                instruction.Restrictions = GetValueOrDefault<List<Guid>>( instructionDict, "restrictions" );
                instruction.Dependencies = GetValueOrDefault<List<Guid>>( instructionDict, "dependencies" );
                instruction.Source = GetValueOrDefault<List<string>>( instructionDict, "source" );
                instruction.Destination = GetValueOrDefault<string>( instructionDict, "destination" );
                instructions.Add( instruction );
            }

            return instructions;
        }

        [NotNull]
        private Dictionary<Guid, Option> DeserializeOptions( [CanBeNull] TomlTableArray tomlObject )
        {
            if ( tomlObject is null )
            {
                _ = Logger.LogVerboseAsync( $"No options found for component '{Name}'" );
                return new Dictionary<Guid, Option>();
            }

            var options = new Dictionary<Guid, Option>( 65535 );

            foreach ( TomlTable item in tomlObject )
            {
                if ( item is null ) continue;

                Dictionary<string, object> optionDict = Serializer.ConvertTomlTableToDictionary( item );

                // reminder: ConvertTomlTableToDictionary lowercases all string keys automatically.
                Serializer.DeserializePathInDictionary( optionDict, "source" );
                Serializer.DeserializeGuidDictionary( optionDict, "restrictions" );
                Serializer.DeserializeGuidDictionary( optionDict, "dependencies" );

                var option = new Option
                {
                    Source = GetRequiredValue<List<string>>( optionDict, "source" ),
                    Guid = GetValueOrDefault<Guid>( optionDict, "guid" ),
                    Destination = GetRequiredValue<string>( optionDict, "destination" ),
                    Restrictions = GetValueOrDefault<List<Guid>>( optionDict, "restrictions" ),
                    Dependencies = GetValueOrDefault<List<Guid>>( optionDict, "dependencies" )
                };

                options.Add( Guid.NewGuid(), option ); // Generate a new GUID key for each option
            }

            return options;
        }

        [NotNull]
        private static T GetRequiredValue<T>(
            [NotNull] IReadOnlyDictionary<string, object> dict,
            [NotNull] string key
        ) =>
            GetValue<T>( dict, key, true );

        [CanBeNull]
        private static T GetValueOrDefault<T>(
            [NotNull] IReadOnlyDictionary<string, object> dict,
            [NotNull] string key
        ) =>
            GetValue<T>( dict, key, false );

        // why did I do this...
        [CanBeNull]
        private static T GetValue<T>(
            [NotNull] IReadOnlyDictionary<string, object> dict,
            [NotNull] string key,
            bool required
        )
        {
            if ( dict is null ) throw new ArgumentNullException( nameof( dict ) );
            if ( key is null ) throw new ArgumentNullException( nameof( key ) );

            if ( !dict.TryGetValue( key, out object value ) )
            {
                string caseInsensitiveKey
                    = dict.Keys.FirstOrDefault( k =>
                        !( k is null )
                        && k.Equals( key, StringComparison.OrdinalIgnoreCase )
                    );

                if ( !dict.TryGetValue( caseInsensitiveKey ?? string.Empty, out object val2 ) )
                {
                    if ( !required )
                    {
                        return default;
                    }
                }

                value = val2;
            }

            switch ( value )
            {
                case null:
                    throw new KeyNotFoundException( $"[Error] Missing or invalid '{key}' field." );
                case T t:
                    return t;
                case string valueStr2 when string.IsNullOrEmpty( valueStr2 ):
                    {
                        if ( required )
                        {
                            throw new KeyNotFoundException( $"'{key}' field cannot be empty." );
                        }

                        return default;
                    }
                case string guidStr when typeof( T ) == typeof( Guid ):
                    {
                        guidStr = Serializer.FixGuidString( guidStr );
                        if ( !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out Guid guid ) )
                        {
                            return (T)(object)guid;
                        }

                        if ( required )
                        {
                            throw new ArgumentException( $"'{key}' field is not a valid Guid!" );
                        }

                        return (T)(object)Guid.Empty;
                    }
            }

            // probably some sort of array at this point
            var valueTomlArray = value as Tomlyn.Model.TomlArray;
            var valueList = value as IList;

            Type targetType = value.GetType();

            if ( targetType.IsGenericType
                && ( targetType.GetGenericTypeDefinition() == typeof( IList<> ) || targetType.GetGenericTypeDefinition() == typeof( List<> ) )
                || valueTomlArray != null
                || valueList != null )
            {
                var valueEnumerable = new List<object>( valueTomlArray ?? (dynamic)valueList );
                Type elementType = typeof( T ).GetGenericArguments()[0];
                dynamic dynamicList = Activator.CreateInstance( typeof( List<> ).MakeGenericType( elementType ) )
                    ?? throw new InvalidOperationException( nameof( dynamicList ) );

                foreach ( object item in valueEnumerable )
                {
                    // handle guid possibility first
                    if ( elementType == typeof( Guid ) && item is string guidString )
                    {
                        guidString = Serializer.FixGuidString( guidString );
                        if ( !string.IsNullOrEmpty( guidString ) )
                        {
                            dynamicList.Add( Guid.Parse( guidString ) );
                        }
                        else if ( !required )
                        {
                            dynamicList.Add( Guid.Empty );
                        }
                        else
                        {
                            throw new InvalidCastException( $"'{key}' field is not a list of valid Guids!" );
                        }
                    }
                    else // simpler types
                    {
                        dynamic convertedItem = Convert.ChangeType( item, elementType );
                        dynamicList.Add( convertedItem );
                    }
                }

                return dynamicList;
            }

            try
            {
                return (T)Convert.ChangeType( value, typeof( T ) );
            }
            catch ( Exception )
            {
                if ( required )
                {
                    throw;
                }
            }

            return default;
        }


        [CanBeNull]
        public static Component DeserializeTomlComponent( [NotNull] string tomlString )
        {
            if ( tomlString is null ) throw new ArgumentNullException( nameof( tomlString ) );

            tomlString = Serializer.FixWhitespaceIssues( tomlString );

            // Can't be bothered to find a real fix when this works fine.
            tomlString = tomlString.Replace( "Instructions = []", "" );
            tomlString = tomlString.Replace( "Options = []", "" );

            // Parse the TOML syntax into a TomlTable
            DocumentSyntax tomlDocument = Toml.Parse( tomlString );

            // Print any errors on the syntax
            if ( tomlDocument.HasErrors )
            {
                foreach ( DiagnosticMessage message in tomlDocument.Diagnostics )
                {
                    if ( message is null ) continue;

                    Logger.Log( message.Message );
                }

                return null;
            }

            TomlTable tomlTable = tomlDocument.ToModel();

            // Get the array of Component tables

            var component = new Component();

            // Deserialize each TomlTable into a Component object
            if ( !( tomlTable["thisMod"] is TomlTableArray componentTables ) )
            {
                return component;
            }

            foreach ( TomlTable tomlComponent in componentTables )
            {
                if ( tomlComponent is null ) continue;

                component.DeserializeComponent( tomlComponent );
            }

            return component;
        }

        [NotNull]
        public static List<Component> ReadComponentsFromFile( [NotNull] string filePath )
        {
            if ( filePath is null ) throw new ArgumentNullException( nameof( filePath ) );

            try
            {
                // Read the contents of the file into a string
                string tomlString = File.ReadAllText( filePath )
                    // the code expects instructions to always be defined. When it's not, code errors and prevents a save.
                    // make the user experience better by just removing the empty instructions key.
                    .Replace( "Instructions = []", string.Empty )
                    .Replace( "Options = []", string.Empty );

                tomlString = Serializer.FixWhitespaceIssues( tomlString );

                // Parse the TOML syntax into a TomlTable
                DocumentSyntax tomlDocument = Toml.Parse( tomlString );

                // Print any errors on the syntax
                if ( tomlDocument.HasErrors )
                {
                    foreach ( DiagnosticMessage message in tomlDocument.Diagnostics )
                    {
                        if ( message is null ) continue;

                        Logger.LogException( new FormatException( message.Message ) );
                    }
                }

                TomlTable tomlTable = tomlDocument.ToModel();

                // Get the array of Component tables
                var componentTables = (TomlTableArray)tomlTable["thisMod"];
                var components = new List<Component>( 65535 );
                // Deserialize each TomlTable into a Component object
                foreach ( TomlTable tomlComponent in componentTables )
                {
                    var thisComponent = new Component();
                    if ( !( tomlComponent is null ) )
                    {
                        thisComponent.DeserializeComponent( tomlComponent );
                    }

                    components.Add( thisComponent );

                    if ( thisComponent.Instructions.Count == 0 )
                    {
                        Logger.Log( $"'{thisComponent.Name}' is missing instructions" );
                        continue;
                    }

                    foreach ( Instruction instruction in thisComponent.Instructions )
                    {
                        instruction?.SetParentComponent( thisComponent );
                    }
                }

                return components;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, "There was a problem serializing the components in the file." );
                throw;
            }
        }

        public enum InstallExitCode
        {
            [Description( "Completed Successfully" )]
            Success,

            [Description( "A dependency or restriction violation between components has occured." )]
            DependencyViolation,

            [Description( "User cancelled the installation." )]
            UserCancelledInstall,

            [Description( "An invalid operation was attempted." )]
            InvalidOperation,

            [Description( "An unexpected exception was thrown" )]
            UnexpectedException,

            [Description( "A tslpatcher error was thrown" )]
            UnknownError,
            TSLPatcherError,

            [Description(
                "The files in the install directory do not match the expected contents provided by the instructions file"
            )]
            ValidationPostInstallMismatch
        }

        public async Task<InstallExitCode> InstallAsync( [CanBeNull] List<Component> componentsList )
        {
            try
            {
                (InstallExitCode, Dictionary<SHA1, FileInfo>) result = await ExecuteInstructionsAsync( componentsList );
                await Logger.LogVerboseAsync( (string)Utility.Utility.GetEnumDescription( result.Item1 ) );
                return result.Item1;
            }
            catch ( InvalidOperationException ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                await Logger.LogErrorAsync(
                    "The above exception is not planned and has not been experienced."
                    + " Please report this to the developer."
                );
            }

            return InstallExitCode.UnknownError;
        }

        private async Task<(InstallExitCode, Dictionary<SHA1, FileInfo>)> ExecuteInstructionsAsync(
            [NotNull] List<Component> componentsList
        )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            if ( !ShouldInstallComponent( componentsList ) )
            {
                return (InstallExitCode.DependencyViolation, null);
            }

            if ( Instructions is null ) return (InstallExitCode.Success, new Dictionary<SHA1, FileInfo>());

            for ( int instructionIndex = 1; instructionIndex <= Instructions.Count; instructionIndex++ )
            {
                int index = instructionIndex;
                Instruction instruction = Instructions[instructionIndex - 1];

                if ( !ShouldRunInstruction( instruction, componentsList ) )
                {
                    continue;
                }

                // Get the original check-sums before making any modifications
                /*await Logger.LogAsync( "Checking file hashes of the install location for mismatch..." );
                var preinstallChecksums = MainConfig.DestinationPath.GetFiles( "*.*", SearchOption.AllDirectories )
                    .ToDictionary( file => file, file => FileChecksumValidator.CalculateSha1Async( file ).Result );

                if ( instruction.OriginalChecksums is null )
                {
                    _ = Logger.LogVerboseAsync(
                        "Instructions file has no checksums available, storing checksums for the current install location as the default checksums."
                    );
                    instruction.OriginalChecksums = preinstallChecksums;
                }
                else if ( !instruction.OriginalChecksums.SequenceEqual( preinstallChecksums ) )
                {
                    await Logger.LogAsync( "Warning! Original checksums of your KOTOR directory do not match the expected checksums from the instructions file." );

                    string message =
                        "WARNING! Some files from your install directory do not match what is expected." + Environment.NewLine
                        + "This usually happens when there's unexpected components already installed." + Environment.NewLine
                        + "Ensure your install location has the required mods installed (usually you want to start with the Vanilla (factory) defaults)";

                    bool? confirmationResult = await PromptUserInstallError( message );
                    switch ( confirmationResult )
                    {
                        // repeat instruction
                        case true:
                            instructionIndex -= 1;
                            continue;

                        // execute next instruction
                        case false:
                            continue;

                        // case null: cancel installing this mod (user closed confirmation dialog)
                        default:
                            return (InstallExitCode.UserCancelledInstall, null);
                    }
                }*/

#pragma warning disable IDE0008
                var exitCode = Instruction.ActionExitCode.Success;
#pragma warning restore IDE0008

                switch ( instruction.Action.ToLower() )
                {
                    case "extract":
                        instruction.SetRealPaths();
                        exitCode = await instruction.ExtractFileAsync();
                        break;
                    case "delete":
                        instruction.SetRealPaths();
                        exitCode = instruction.DeleteFile();
                        break;
                    case "delduplicate":
                        instruction.SetRealPaths( true );
                        instruction.DeleteDuplicateFile();
                        exitCode = Instruction.ActionExitCode.Success;
                        break;
                    case "copy":
                        instruction.SetRealPaths();
                        exitCode = instruction.CopyFile();
                        break;
                    case "move":
                        instruction.SetRealPaths();
                        exitCode = instruction.MoveFile();
                        break;
                    case "rename":
                        exitCode = instruction.RenameFile();
                        break;
                    case "patch":
                    case "holopatcher":
                    case "tslpatcher":
                        instruction.SetRealPaths();
                        exitCode = await instruction.ExecuteTSLPatcherAsync();
                        break;
                    case "execute":
                    case "run":
                        instruction.SetRealPaths();
                        exitCode = await instruction.ExecuteProgramAsync();
                        break;

                    case "choose":
                        instruction.SetRealPaths();
                        List<Option> options = ChooseOptions( instruction );
                        List<string> optionNames = options.ConvertAll( option => option.Name );

                        string selectedOptionName = await OptionsCallback.ShowOptionsDialog( optionNames )
                            ?? throw new NullReferenceException( nameof( optionNames ) );
                        Option selectedOption = null;

                        foreach ( Option option in options )
                        {
                            string optionName = option.Name;
                            if ( optionName != selectedOptionName )
                            {
                                continue;
                            }

                            selectedOption = option;
                            break;
                        }

                        if ( selectedOption != null )
                        {
                            ChosenOptions.Add( selectedOption );
                        }

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
                    default:
                        // Handle unknown instruction type here
                        await Logger.LogWarningAsync( $"Unknown instruction '{instruction.Action}'" );
                        exitCode = Instruction.ActionExitCode.UnknownInstruction;
                        break;
                }


                _ = Logger.LogVerboseAsync(
                    $"Instruction #{instructionIndex} '{instruction.Action}' exited with code {Instruction.ActionExitCode.Success}"
                );
                if ( exitCode != Instruction.ActionExitCode.Success )
                {
                    await Logger.LogErrorAsync(
                        $"FAILED Instruction #{instructionIndex} Action '{instruction.Action}'"
                    );
                    bool? confirmationResult = await PromptUserInstallError(
                        $"An error occurred during the installation of '{Name}':"
                        + Environment.NewLine
                        + Utility.Utility.GetEnumDescription( Instruction.ActionExitCode.Success )
                    );

                    switch ( confirmationResult )
                    {
                        // repeat instruction
                        case true:
                            instructionIndex -= 1;
                            continue;

                        // execute next instruction
                        case false:
                            continue;

                        // case null: cancel installing this mod (user closed confirmation dialog)
                        default:
                            return (InstallExitCode.UserCancelledInstall, null);
                    }
                }

                /*if (instruction.ExpectedChecksums != null)
                {
                    // Get the new checksums after the modifications
                    var validator = new FileChecksumValidator(
                        destinationPath: MainConfig.DestinationPath.FullName,
                        expectedChecksums: instruction.ExpectedChecksums,
                        originalChecksums: preinstallChecksums
                    );
                    bool checksumsMatch = await validator.ValidateChecksumsAsync();
                    if (!checksumsMatch)
                    {
                        _ = Logger.LogWarningAsync($"Component '{this.Name}' instruction #{instructionIndex} '{instruction.Action}' succeeded but modified files have unexpected checksums.");
                        return (InstallExitCode.ValidationPostInstallMismatch, preinstallChecksums);
                    }

                    _ = Logger.LogVerboseAsync($"Component '{this.Name}' instruction #{instructionIndex} '{instruction.Action}': The modified files have expected checksums.");
                }
                else
                {
                    _ = Logger.LogAsync($"Component '{this.Name}' instruction #{instructionIndex} '{instruction.Action}' ran, saving the new checksums as expected.");
                    var newChecksums = new Dictionary<FileInfo, System.Security.Cryptography.SHA1>();
                    foreach (FileInfo file in MainConfig.DestinationPath.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        System.Security.Cryptography.SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async(file);
                        newChecksums[file] = sha1;
                    }
                }*/

                _ = Logger.LogAsync( $"Successfully completed instruction #{instructionIndex} '{instruction.Action}'" );

                async Task<bool?> PromptUserInstallError( string message )
                {
                    return await ConfirmCallback.ShowConfirmationDialog(
                        message
                        + Environment.NewLine
                        + $"Instruction #{index} action '{instruction.Action}'"
                        + Environment.NewLine
                        + "Retry this Instruction?"
                        + Environment.NewLine
                        + Environment.NewLine
                        + " 'YES': RETRY this Instruction"
                        + Environment.NewLine
                        + " 'NO':  SKIP this Instruction"
                        + Environment.NewLine
                        + $" or CLOSE THIS WINDOW to ABORT the installation of '{Name}'."
                    );
                }
            }

            return (InstallExitCode.Success, new Dictionary<SHA1, FileInfo>());
        }

        [NotNull]
        public static Dictionary<string, List<Component>> GetConflictingComponents(
            [CanBeNull] List<Guid> dependencyGuids,
            [CanBeNull] List<Guid> restrictionGuids,
            [NotNull] List<Component> componentsList,
            bool isInstall = false
        )
        {
            if ( componentsList == null ) throw new ArgumentNullException( nameof( componentsList ) );

            var conflicts = new Dictionary<string, List<Component>>();

            if ( dependencyGuids != null && dependencyGuids.Count > 0 )
            {
                var dependencyConflicts = new List<Component>();

                foreach ( Guid requiredGuid in dependencyGuids )
                {
                    Component checkComponent = componentsList.FirstOrDefault( c => c.Guid == requiredGuid );

                    if ( checkComponent?.IsSelected == false )
                    {
                        dependencyConflicts.Add( checkComponent );
                    }
                }


                if ( isInstall && dependencyConflicts.Count > 0 )
                {
                    Logger.Log(
                        $"Skipping, required components not selected for install: [{string.Join( ",", dependencyConflicts.Select( component => component.Name ).ToList() )}]"
                    );
                }

                if ( dependencyConflicts.Count > 0 )
                {
                    conflicts["Dependency"] = dependencyConflicts;
                }
            }

            if ( restrictionGuids != null && restrictionGuids.Count > 0 )
            {
                var restrictionConflicts = new List<Component>();

                foreach ( Guid restrictedGuid in restrictionGuids )
                {
                    Component checkComponent = componentsList.FirstOrDefault( c => c.Guid == restrictedGuid );

                    if ( checkComponent?.IsSelected == true )
                    {
                        restrictionConflicts.Add( checkComponent );
                    }
                }

                if ( isInstall && restrictionConflicts.Count > 0 )
                {
                    Logger.Log(
                        $"Skipping due to restricted components in install queue: [{string.Join( ",", restrictionConflicts.Select( component => component.Name ).ToList() )}]"
                    );
                }


                if ( restrictionConflicts.Count > 0 )
                {
                    conflicts["Restriction"] = restrictionConflicts;
                }
            }

            return conflicts;
        }

        //The component will be installed if any of the following conditions are met:
        //The component has no dependencies or restrictions.
        //The component has dependencies, and all of the required components are being installed.
        //The component has restrictions, but none of the restricted components are being installed.
        public bool ShouldInstallComponent( [NotNull] List<Component> componentsList, bool isInstall = true )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            Dictionary<string, List<Component>> conflicts = GetConflictingComponents(
                Dependencies,
                Restrictions,
                componentsList,
                isInstall
            );
            return conflicts.Count == 0;
        }

        //The instruction will run if any of the following conditions are met:
        //The instruction has no dependencies or restrictions.
        //The instruction has dependencies, and all of the required components are being installed.
        //The instruction has restrictions, but none of the restricted components are being installed.
        public static bool ShouldRunInstruction(
            [NotNull] Instruction instruction,
            [NotNull] List<Component> componentsList,
            bool isInstall = true
        )
        {
            if ( instruction is null ) throw new ArgumentNullException( nameof( instruction ) );
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            Dictionary<string, List<Component>> conflicts = GetConflictingComponents(
                instruction.Dependencies,
                instruction.Restrictions,
                componentsList,
                isInstall
            );
            return conflicts.Count == 0;
        }

        [CanBeNull]
        public static Component FindComponentFromGuid( Guid guidToFind, [NotNull] List<Component> componentsList )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            Component foundComponent = null;
            foreach ( Component component in componentsList )
            {
                if ( component.Guid != guidToFind )
                {
                    continue;
                }

                foundComponent = component;
                break;
            }

            return foundComponent;
        }

        public static List<Component> FindComponentsFromGuidList(
            [NotNull] List<Guid> guidsToFind,
            [NotNull] List<Component> componentsList
        )
        {
            if ( guidsToFind is null ) throw new ArgumentNullException( nameof( guidsToFind ) );
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            var foundComponents = new List<Component>();
            foreach ( Guid guidToFind in guidsToFind )
            {
                Component foundComponent = FindComponentFromGuid( guidToFind, componentsList );
                if ( foundComponent is null )
                {
                    continue;
                }

                foundComponents.Add( foundComponent );
            }

            return foundComponents;
        }

        public static (bool isCorrectOrder, List<Component> reorderedComponents) ConfirmComponentsInstallOrder(
            [NotNull] List<Component> components
        )
        {
            if ( components is null ) throw new ArgumentNullException( nameof( components ) );

            Dictionary<Guid, GraphNode> nodeMap = CreateDependencyGraph( components );

            var visitedNodes = new HashSet<GraphNode>();
            var orderedComponents = new List<Component>();

            foreach ( GraphNode node in nodeMap.Values )
            {
                if ( !visitedNodes.Contains( node ) )
                {
                    DepthFirstSearch( node, visitedNodes, orderedComponents );
                }
            }

            bool isCorrectOrder = orderedComponents.SequenceEqual( components );

            return (isCorrectOrder, orderedComponents);
        }

        // use a graph traversal algorithm
        private static void DepthFirstSearch(
            [NotNull] GraphNode node,
            [NotNull] ISet<GraphNode> visitedNodes,
            [NotNull] ICollection<Component> orderedComponents
        )
        {
            if ( node is null ) throw new ArgumentNullException( nameof( node ) );
            if ( visitedNodes is null ) throw new ArgumentNullException( nameof( visitedNodes ) );
            if ( orderedComponents is null ) throw new ArgumentNullException( nameof( orderedComponents ) );

            _ = visitedNodes.Add( node );

            foreach ( GraphNode dependency in node.Dependencies )
            {
                if ( !visitedNodes.Contains( dependency ) )
                {
                    DepthFirstSearch( dependency, visitedNodes, orderedComponents );
                }
            }

            orderedComponents.Add( node.Component );
        }

        private static Dictionary<Guid, GraphNode> CreateDependencyGraph( List<Component> components )
        {
            var nodeMap = new Dictionary<Guid, GraphNode>();

            foreach ( Component component in components )
            {
                var node = new GraphNode( component );
                nodeMap[component.Guid] = node;
            }

            foreach ( Component component in components )
            {
                GraphNode node = nodeMap[component.Guid];

                if ( component.InstallAfter != null )
                {
                    foreach ( Guid dependencyGuid in component.InstallAfter )
                    {
                        GraphNode dependencyNode = nodeMap[dependencyGuid];
                        _ = node.Dependencies.Add( dependencyNode );
                    }
                }

                if ( component.InstallBefore != null )
                {
                    foreach ( Guid dependentGuid in component.InstallBefore )
                    {
                        GraphNode dependentNode = nodeMap[dependentGuid];
                        _ = dependentNode.Dependencies.Add( node );
                    }
                }
            }

            return nodeMap;
        }

        public class GraphNode
        {
            public Component Component { get; }
            public HashSet<GraphNode> Dependencies { get; }

            public GraphNode( [CanBeNull] Component component )
            {
                Component = component;
                Dependencies = new HashSet<GraphNode>();
            }
        }


        private List<Option> ChooseOptions( Instruction instruction )
        {
            if ( instruction is null )
            {
                throw new ArgumentNullException( nameof( instruction ) );
            }

            List<string> archives = Validator.GetAllArchivesFromInstructions( this );

            if ( archives.Count == 0 )
            {
                throw new InvalidOperationException( "No archives found." );
            }

            var selectedOptions = new List<Option>();

            foreach ( Option option in Options.Values )
            {
                foreach ( string sourcePath in option.Source )
                {
                    if ( !archives.Contains( sourcePath ) )
                    {
                        continue;
                    }

                    selectedOptions.Add( option );
                    break;
                }
            }

            return selectedOptions;
        }

        public void CreateInstruction( int index = 0 )
        {
            var instruction = new Instruction();
            if ( Instructions.Count == 0 )
            {
                if ( index != 0 )
                {
                    Logger.Log( "Cannot create instruction at index when list is empty." );
                    return;
                }

                Instructions.Add( instruction );
            }
            else
            {
                Instructions.Insert( index, instruction );
            }
        }

        public void DeleteInstruction( int index ) => Instructions.RemoveAt( index );

        public void MoveInstructionToIndex( Instruction thisInstruction, int index )
        {
            if ( thisInstruction is null || index < 0 || index >= Instructions.Count )
            {
                throw new ArgumentException( "Invalid instruction or index." );
            }

            int currentIndex = Instructions.IndexOf( thisInstruction );
            if ( currentIndex < 0 )
            {
                throw new ArgumentException( "Instruction does not exist in the list." );
            }

            if ( index == currentIndex )
            {
                _ = Logger.LogAsync(
                    $"Cannot move Instruction '{thisInstruction.Action}' from {currentIndex} to {index}. Reason: Indices are the same."
                );
                return;
            }

            Instructions.RemoveAt( currentIndex );
            Instructions.Insert( index, thisInstruction );

            _ = Logger.LogVerboseAsync(
                $"Instruction '{thisInstruction.Action}' moved from {currentIndex} to {index}"
            );
        }

        // used for the ui.
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
