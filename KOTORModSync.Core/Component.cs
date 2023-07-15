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
using Microsoft.CSharp.RuntimeBinder;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
using static KOTORModSync.Core.Utility.CallbackObjects;

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
        [NotNull] public List<Guid> Dependencies { get => _dependencies; set { _dependencies = value; OnPropertyChanged(); } }
        [NotNull] public List<Guid> Restrictions { get => _restrictions; set { _restrictions = value; OnPropertyChanged(); } }


        [NotNull] public List<Guid> InstallBefore { get; set; } = new List<Guid>();
        [NotNull] public List<Guid> InstallAfter { get; set; } = new List<Guid>();
        public bool NonEnglishFunctionality { get; set; }
        [NotNull] public string InstallationMethod { get; set; } = string.Empty;
        [NotNull][ItemNotNull] public List<Instruction> Instructions { get; set; } = new List<Instruction>();
        [NotNull] public Dictionary<Guid, Option> Options { get; set; } = new Dictionary<Guid, Option>();
        [NotNull][ItemNotNull] public List<string> Language { get; private set; } = new List<string>();

        [NotNull] public List<string> ModLink { get; set; } = new List<string>();
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        [NotNull] public Dictionary<Guid, Option> ChosenOptions { get; set; } = new Dictionary<Guid, Option>();

        [NotNull] private List<Guid> _dependencies = new List<Guid>();
        [NotNull] private List<Guid> _restrictions = new List<Guid>();
        private bool _isSelected;

        [NotNull]
        public string SerializeComponent()
        {
            var rootTable = new Dictionary<string, List<object>>( StringComparer.OrdinalIgnoreCase )
            {
                ["thisMod"] = new List<object> { Serializer.SerializeObject( this ) }
            };

            // ReSharper disable once AssignNullToNotNullAttribute
            RemoveEmptyCollections( rootTable["thisMod"] );

            string tomlString = Nett.Toml.WriteString( rootTable );
            if ( string.IsNullOrWhiteSpace( tomlString ) )
                throw new InvalidOperationException( "Could not serialize into a valid toml string" );

            return Serializer.FixWhitespaceIssues( tomlString );
        }

        private void RemoveEmptyCollections( [NotNull] IDictionary<string, object> thisTable )
        {
            if ( thisTable is null )
                throw new ArgumentNullException( nameof( thisTable ) );

            var itemsToRemove = new List<string>();

            foreach ( KeyValuePair<string, object> kvp in thisTable )
            {
                if ( kvp.Key == null )
                    continue;

                switch ( kvp.Value )
                {
                    case IEnumerable enumerable when !enumerable.GetEnumerator().MoveNext():
                        itemsToRemove.Add( kvp.Key );
                        continue;
                    case IDictionary<string, object> table:
                        {
                            var emptyKeys = table.Keys.Where( key =>
                            {
                                if ( key is null )
                                    return default;

                                object value = table[key];
                                switch ( value )
                                {
                                    case IList<object> list:
                                        RemoveEmptyCollections( list ); // Recursively check sub-lists
                                        return list.Count == 0;
                                    case IDictionary<string, object> dict:
                                        RemoveEmptyCollections( dict ); // Recursively check sub-dictionaries
                                        return dict.Count == 0;
                                    default:
                                        return false;
                                }
                            } ).ToList();

                            foreach ( string key in emptyKeys )
                            {
                                if ( !( key is null ) )
                                {
                                    _ = table.Remove( key );
                                }
                            }

                            if ( table.Count == 0 )
                            {
                                itemsToRemove.Add( table.GetHashCode().ToString() );
                            }

                            break;
                        }
                    case IList<object> list:
                        RemoveEmptyCollections( list ); // Recursively check sub-lists
                        break;
                }
            }

            // Remove items from the rootTable
            foreach ( string item in itemsToRemove )
            {
                if ( item is null )
                    continue;

                _ = thisTable.Remove( item );
            }
        }

        private void RemoveEmptyCollections( [NotNull] IList<object> list )
        {
            if ( list is null )
                throw new ArgumentNullException( nameof( list ) );

            for ( int i = list.Count - 1; i >= 0; i-- )
            {
                object item = list[i];
                switch ( item )
                {
                    case IDictionary<string, object> dict:
                        {
                            RemoveEmptyCollections( dict ); // Recursively check sub-dictionaries
                            if ( dict.Count == 0 )
                                list.RemoveAt( i );

                            break;
                        }
                    case IList<object> subList:
                        {
                            RemoveEmptyCollections( subList ); // Recursively check sub-lists
                            if ( subList.Count == 0 )
                                list.RemoveAt( i );

                            break;
                        }
                }
            }
        }


        public void DeserializeComponent( [NotNull] IDictionary<string, object> componentDict )
        {
            if ( !( componentDict is TomlTable _ ) )
            {
                throw new ArgumentException( "[TomlError] Expected a TOML table for component data." );
            }

            _tempPath = new DirectoryInfo( Path.GetTempPath() );

            Name = GetRequiredValue<string>( componentDict, "Name" );
            _ = Logger.LogAsync( $"{Environment.NewLine}== Deserialize next component '{Name}' ==" );
            Guid = GetRequiredValue<Guid>( componentDict, "Guid" );
            Description = GetValueOrDefault<string>( componentDict, "Description" ) ?? string.Empty;
            Directions = GetValueOrDefault<string>( componentDict, "Directions" ) ?? string.Empty;
            Category = GetValueOrDefault<string>( componentDict, "Category" ) ?? string.Empty;
            Tier = GetValueOrDefault<string>( componentDict, "Tier" ) ?? string.Empty;
            Language = GetValueOrDefault<List<string>>( componentDict, "Language" ) ?? new List<string>();
            Author = GetValueOrDefault<string>( componentDict, "Author" ) ?? string.Empty;
            Dependencies = GetValueOrDefault<List<Guid>>( componentDict, "Dependencies" ) ?? new List<Guid>();
            Restrictions = GetValueOrDefault<List<Guid>>( componentDict, "Restrictions" ) ?? new List<Guid>();
            InstallBefore = GetValueOrDefault<List<Guid>>( componentDict, "InstallBefore" ) ?? new List<Guid>();
            InstallAfter = GetValueOrDefault<List<Guid>>( componentDict, "InstallAfter" ) ?? new List<Guid>();

            ModLink = GetValueOrDefault<List<string>>( componentDict, "ModLink" ) ?? new List<string>();
            if ( ModLink.Count == 0 )
            {
                string modLink = GetValueOrDefault<string>( componentDict, "ModLink" ) ?? string.Empty;
                if ( string.IsNullOrEmpty( modLink ) )
                    Logger.LogError( "Could not deserialize key 'ModLink'" );
                else
                    ModLink = modLink.Split( new[] { "\r\n", "\n" }, StringSplitOptions.None ).ToList();
            }

            IsSelected = GetValueOrDefault<bool>( componentDict, "IsSelected" );

            Instructions = DeserializeInstructions( GetValueOrDefault<IList<object>>( componentDict, "Instructions" ) );
            Instructions.ForEach( instruction => instruction?.SetParentComponent( this ) );

            /*Options = DeserializeOptions(
                GetValueOrDefault<IList<IDictionary<string, object>>>( componentDict, "Options" )
            );*/

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

            var stringBuilder = new StringBuilder();

            foreach ( Component thisComponent in components )
            {
                _ = stringBuilder.AppendLine( thisComponent.SerializeComponent() );
            }

            string tomlString = stringBuilder.ToString();
            File.WriteAllText( filePath, tomlString );
        }

        public static string GenerateModDocumentation( [NotNull][ItemNotNull] List<Component> componentsList )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            var sb = new StringBuilder();
            const string indentation = "    ";

            // Loop through each 'thisMod' entry
            foreach ( Component component in componentsList )
            {
                _ = sb.AppendLine();

                // Component Information
                _ = sb.Append( "####**" )
                    .Append( component.Name )
                    .AppendLine( "**" );
                _ = sb.Append( "**Author**: " )
                    .AppendLine( component.Author );
                _ = sb.AppendLine();
                _ = sb.Append( "**Description**: " )
                    .AppendLine( component.Description );
                _ = sb.Append( "**Tier & Category**: " )
                    .Append( component.Tier )
                    .Append( " - " )
                    .AppendLine( component.Category );
                _ = string.Equals( component.Language.FirstOrDefault(), "All", StringComparison.OrdinalIgnoreCase )
                    ? sb.AppendLine( "**Supported Languages**: ALL" )
                    : sb.Append( "**Supported Languages**: [" )
                        .AppendLine()
                        .Append(
                            string.Join(
                                $",{Environment.NewLine}",
                                component.Language.Select( item => $"{indentation}{item}" )
                            )
                        )
                        .AppendLine()
                        .Append( ']' )
                        .AppendLine();

                _ = sb.Append( "**Directions**: " )
                    .AppendLine( component.Directions );

                // Instructions
                _ = sb.AppendLine();
                _ = sb.AppendLine( "**Installation Instructions:**" );
                foreach (
                    Instruction instruction in component.Instructions.Where(
                        instruction => instruction.Action != "extract"
                    )
                )
                {
                    _ = sb.Append( "**Action**: " )
                        .AppendLine( instruction?.Action );
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
                        _ = sb.Append( "Destination: " )
                            .AppendLine( instruction.Destination );
                    }
                }
            }

            return sb.ToString();
        }

        [ItemNotNull]
        [NotNull]
        private List<Instruction> DeserializeInstructions(
            [CanBeNull][ItemCanBeNull] IList<object> instructionsSerializedList
        )
        {
            if ( instructionsSerializedList is null || instructionsSerializedList.Count == 0 )
            {
                _ = Logger.LogWarningAsync( $"No instructions found for component '{Name}'" );
                return new List<Instruction>();
            }

            var instructions = new List<Instruction>();

            for ( int index = 0; index < instructionsSerializedList.Count; index++ )
            {
                var instructionDict = (IDictionary<string, object>)instructionsSerializedList[index];
                if ( instructionDict is null ) continue;

                Serializer.DeserializePathInDictionary( instructionDict, "Source" );
                Serializer.DeserializeGuidDictionary( instructionDict, "Restrictions" );
                Serializer.DeserializeGuidDictionary( instructionDict, "Dependencies" );

                var instruction = new Instruction();
                instruction.Action = GetRequiredValue<string>( instructionDict, "Action" );
                _ = Logger.LogAsync(
                    $"{Environment.NewLine}-- Deserialize instruction #{index + 1} action {instruction.Action}"
                );
                instruction.Arguments = GetValueOrDefault<string>( instructionDict, "Arguments" );
                instruction.Overwrite = GetValueOrDefault<bool>( instructionDict, "Overwrite" );

                instruction.Restrictions
                    = GetValueOrDefault<List<Guid>>( instructionDict, "Restrictions" ) ?? new List<Guid>();
                instruction.Dependencies
                    = GetValueOrDefault<List<Guid>>( instructionDict, "Dependencies" ) ?? new List<Guid>();
                instruction.Source = GetValueOrDefault<List<string>>( instructionDict, "Source" ) ?? new List<string>();
                instruction.Destination = GetValueOrDefault<string>( instructionDict, "Destination" );
                instructions.Add( instruction );
            }

            return instructions;
        }

        [ItemNotNull]
        [NotNull]
        private List<Option> DeserializeOptions(
            [CanBeNull][ItemCanBeNull] IList<object> optionsSerializedList
        )
        {
            if ( optionsSerializedList is null || optionsSerializedList.Count == 0 )
            {
                _ = Logger.LogWarningAsync( $"No options found for component '{Name}'" );
                return new List<Option>();
            }

            var options = new List<Option>();

            for ( int index = 0; index < optionsSerializedList.Count; index++ )
            {
                var optionsDict = (IDictionary<string, object>)optionsSerializedList[index];
                if ( optionsDict is null ) continue;

                Serializer.DeserializeGuidDictionary( optionsDict, "Restrictions" );
                Serializer.DeserializeGuidDictionary( optionsDict, "Dependencies" );

                var option = new Option();
                _ = Logger.LogAsync(
                    $"{Environment.NewLine}-- Deserialize option #{index + 1}"
                );

                option.Name = GetRequiredValue<string>( optionsDict, "Name" );
                _ = Logger.LogAsync( $"{Environment.NewLine}== Deserialize next component '{Name}' ==" );
                option.Guid = GetRequiredValue<Guid>( optionsDict, "Guid" );
                option.Restrictions
                    = GetValueOrDefault<List<Guid>>( optionsDict, "Restrictions" ) ?? new List<Guid>();
                option.Dependencies
                    = GetValueOrDefault<List<Guid>>( optionsDict, "Dependencies" ) ?? new List<Guid>();
                option.Instructions
                    = Instructions = DeserializeInstructions( GetValueOrDefault<IList<object>>( optionsDict, "Instructions" ) );
                options.Add( option );
            }

            return options;
        }

        [NotNull]
        private static T GetRequiredValue<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key )
        {
            T value = GetValue<T>( dict, key, true );
            // ReSharper disable once CompareNonConstrainedGenericWithNull
            return value == null
                ? throw new InvalidOperationException( "GetValue cannot return null for a required value." )
                : value;
        }


        [CanBeNull]
        private static T GetValueOrDefault<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key ) =>
            GetValue<T>( dict, key, false );

        // why did I do this...
        [CanBeNull]
        private static T GetValue<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key, bool required )
        {
            try
            {
                if ( dict is null ) throw new ArgumentNullException( nameof( dict ) );
                if ( key is null ) throw new ArgumentNullException( nameof( key ) );

                if ( !dict.TryGetValue( key, out object value ) )
                {
                    string caseInsensitiveKey = dict.Keys.FirstOrDefault(
                        k => !( k is null ) && k.Equals( key, StringComparison.OrdinalIgnoreCase )
                    );

                    if ( !dict.TryGetValue( caseInsensitiveKey ?? string.Empty, out object val2 ) && !required )
                    {
                        return default;
                    }

                    value = val2;
                }

                Type targetType = typeof( T );

                switch ( value )
                {
                    case null:
                        throw new KeyNotFoundException( $"[Error] Missing or invalid '{key}' field." );
                    case T t:
                        return t;
                    case string valueStr when string.IsNullOrEmpty( valueStr ):
                        {
                            if ( required )
                            {
                                throw new KeyNotFoundException( $"'{key}' field cannot be empty." );
                            }

                            return default;
                        }
                    case string valueStr when targetType == typeof( Guid ):
                        {
                            string guidStr = Serializer.FixGuidString( valueStr );
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
                    case string valueStr when targetType == typeof( string ):
                        return (T)(object)valueStr;
                }

                // probably some sort of array at this point
                var tomlArray = value as IList<object>;
                var tomlTableArray = value as IList<TomlTable>;

                dynamic valueEnumerable = tomlArray;
                valueEnumerable = valueEnumerable ?? tomlTableArray;
                if ( valueEnumerable != null )
                {
                    Type[] genericArgs = typeof( T ).GetGenericArguments();
                    Type elementType = genericArgs.Length > 0
                        ? genericArgs[0]
                        : typeof( string );
                    Type listType = typeof( List<> ).MakeGenericType( elementType );
                    dynamic dynamicList = Activator.CreateInstance( listType )
                        ?? throw new InvalidOperationException( nameof( dynamicList ) );
                    foreach ( object item in valueEnumerable )
                    {
                        if ( elementType == typeof( Guid ) && item is string guidString )
                        {
                            guidString = Serializer.FixGuidString( guidString );
                            Guid parsedGuid = string.IsNullOrEmpty( guidString )
                                ? Guid.Empty
                                : Guid.Parse( guidString );
                            dynamicList.Add( (dynamic)parsedGuid );
                        }
                        else
                        {
                            dynamicList.Add( (dynamic)item );
                        }
                    }

                    return (T)dynamicList;
                }


                try
                {
                    return (T)Convert.ChangeType( value, typeof( T ) );
                }
                catch ( Exception e )
                {
                    Logger.LogError( $"Could not deserialize key '{key}'" );
                    if ( required )
                        throw;

                    Logger.LogException( e );
                }
            }
            catch ( RuntimeBinderException )
            {
                if ( required )
                    throw;

                return default;
            }
            catch ( InvalidCastException )
            {
                if ( required )
                    throw;

                return default;
            }

            return default;
        }


        [CanBeNull]
        public static Component DeserializeTomlComponent( [NotNull] string tomlString )
        {
            if ( tomlString is null ) throw new ArgumentNullException( nameof( tomlString ) );

            tomlString = Serializer.FixWhitespaceIssues( tomlString );

            // Parse the TOML syntax into a IDictionary<string, object>
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

            IDictionary<string, object> tomlTable = tomlDocument.ToModel();

            // Get the array of Component tables
            var component = new Component();
            if ( !( tomlTable["thisMod"] is IList<TomlTable> componentTables ) )
            {
                return component;
            }

            // Deserialize each IDictionary<string, object> into a Component object
            foreach ( TomlTable tomlComponent in componentTables )
            {
                if ( !( tomlComponent is IDictionary<string, object> componentDict ) ) continue;

                component.DeserializeComponent( componentDict );
            }

            return component;
        }


        [NotNull]
        [ItemNotNull]
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

                if ( string.IsNullOrWhiteSpace( tomlString ) )
                {
                    throw new Nett.Parser.ParseException(
                        $"Expected an instructions file at '{filePath}' but the file was empty."
                    );
                }

                tomlString = Serializer.FixWhitespaceIssues( tomlString );

                // Parse the TOML syntax into a IDictionary<string, object>
                DocumentSyntax tomlDocument = Toml.Parse( tomlString );

                // Print any errors on the syntax
                if ( tomlDocument.HasErrors )
                {
                    foreach ( DiagnosticMessage message in tomlDocument.Diagnostics )
                    {
                        if ( message is null )
                            continue;

                        Logger.LogError( message.Message );
                    }
                }

                IDictionary<string, object> tomlTable = tomlDocument.ToModel();

                // Get the array of Component tables
                var componentTables = (IList<TomlTable>)tomlTable["thisMod"];
                var components = new List<Component>();
                // Deserialize each IDictionary<string, object> into a Component object
                if ( componentTables is null )
                    return components;

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
            UnknownError,

            [Description( "A tslpatcher error was thrown" )]
            TSLPatcherError,

            [Description(
                "The files in the install directory do not match the expected contents provided by the instructions file"
            )]
            ValidationPostInstallMismatch
        }

        public async Task<InstallExitCode> InstallAsync( [NotNull] List<Component> componentsList )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            try
            {
                (InstallExitCode, Dictionary<SHA1, FileInfo>) result = await ExecuteInstructionsAsync( Instructions, componentsList );
                await Logger.LogAsync( (string)Utility.Utility.GetEnumDescription( result.Item1 ) );
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
            [NotNull][ItemNotNull] List<Instruction> theseInstructions,
            [NotNull][ItemNotNull] List<Component> componentsList
        )
        {
            if ( theseInstructions is null )
                throw new ArgumentNullException( nameof( theseInstructions ) );
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            if ( !ShouldInstallComponent( componentsList ) )
            {
                return (InstallExitCode.DependencyViolation, null);
            }

            InstallExitCode installExitCode = InstallExitCode.Success;

            for ( int instructionIndex = 1; instructionIndex <= theseInstructions.Count; instructionIndex++ )
            {
                int index = instructionIndex;
                Instruction instruction = theseInstructions[instructionIndex - 1];

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

                Instruction.ActionExitCode exitCode = Instruction.ActionExitCode.Success;
                switch ( instruction.Action?.ToLower() )
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

                    case "option":
                        instruction.SetRealPaths();

                        Option chosenOption = instruction.GetChosenOption();
                        (installExitCode, _) = await ExecuteInstructionsAsync( chosenOption.Instructions, componentsList );
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

            return (installExitCode, new Dictionary<SHA1, FileInfo>());
        }

        [NotNull]
        public static Dictionary<string, List<Component>> GetConflictingComponents(
            [NotNull] List<Guid> dependencyGuids,
            [NotNull] List<Guid> restrictionGuids,
            [NotNull][ItemNotNull] List<Component> componentsList,
            bool isInstall = false
        )
        {
            if ( dependencyGuids is null ) throw new ArgumentNullException( nameof( dependencyGuids ) );
            if ( restrictionGuids is null ) throw new ArgumentNullException( nameof( restrictionGuids ) );
            if ( componentsList == null ) throw new ArgumentNullException( nameof( componentsList ) );

            var conflicts = new Dictionary<string, List<Component>>();

            if ( dependencyGuids.Count > 0 )
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

            if ( restrictionGuids.Count > 0 )
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
        public bool ShouldInstallComponent( [NotNull][ItemNotNull] List<Component> componentsList, bool isInstall = true )
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
        public static Component FindComponentFromGuid( Guid guidToFind, [NotNull][ItemNotNull] List<Component> componentsList )
        {
            if ( componentsList is null ) throw new ArgumentNullException( nameof( componentsList ) );

            Component foundComponent = null;
            foreach ( Component component in componentsList )
            {
                if ( component.Guid != guidToFind )
                    continue;

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
                    continue;

                foundComponents.Add( foundComponent );
            }

            return foundComponents;
        }

        public static (bool isCorrectOrder, List<Component> reorderedComponents) ConfirmComponentsInstallOrder(
            [NotNull][ItemNotNull] List<Component> components
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
                if ( visitedNodes.Contains( dependency ) )
                    continue;

                DepthFirstSearch( dependency, visitedNodes, orderedComponents );
            }

            orderedComponents.Add( node.Component );
        }

        private static Dictionary<Guid, GraphNode> CreateDependencyGraph( [NotNull][ItemNotNull] List<Component> components )
        {
            if ( components is null )
                throw new ArgumentNullException( nameof( components ) );

            var nodeMap = new Dictionary<Guid, GraphNode>();

            foreach ( Component component in components )
            {
                var node = new GraphNode( component );
                nodeMap[component.Guid] = node;
            }

            foreach ( Component component in components )
            {
                GraphNode node = nodeMap[component.Guid];

                foreach ( Guid dependencyGuid in component.InstallAfter )
                {
                    GraphNode dependencyNode = nodeMap[dependencyGuid];
                    _ = node?.Dependencies?.Add( dependencyNode );
                }

                foreach ( Guid dependentGuid in component.InstallBefore )
                {
                    GraphNode dependentNode = nodeMap[dependentGuid];
                    _ = dependentNode?.Dependencies?.Add( node );
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

        public void CreateInstruction( int index = 0 )
        {
            var instruction = new Instruction();
            if ( Instructions.Count == 0 )
            {
                if ( index != 0 )
                {
                    Logger.LogError( "Cannot create instruction at index when list is empty." );
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

        protected virtual void OnPropertyChanged( [CallerMemberName] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
