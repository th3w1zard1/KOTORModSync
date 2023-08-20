// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemPathing;
using KOTORModSync.Core.Utility;
using Microsoft.CSharp.RuntimeBinder;
using SharpCompress;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

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
            ComponentInstallationFailed,
        }

        /*
        public DateTime SourceLastModified { get; set; }
        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        */

        
        [NotNull] private string _name = string.Empty;
        [NotNull] public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }
        
        private Guid _guid;
        public Guid Guid
        {
            get => _guid;
            set
            {
                _guid = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _author = string.Empty;
        [NotNull] public string Author
        {
            get => _author;
            set
            {
                _author = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _category = string.Empty;
        [NotNull] public string Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _tier = string.Empty;
        [NotNull] public string Tier
        {
            get => _tier;
            set
            {
                _tier = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _description = string.Empty;
        [NotNull] public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private string _directions = string.Empty;
        [NotNull] public string Directions
        {
            get => _directions;
            set
            {
                _directions = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private List<Guid> _dependencies = new List<Guid>();
        [NotNull] public List<Guid> Dependencies
        {
            get => _dependencies;
            set
            {
                _dependencies = value;
                OnPropertyChanged();
            }
        }
        
        [NotNull] private List<Guid> _restrictions = new List<Guid>();
        [NotNull] public List<Guid> Restrictions
        {
            get => _restrictions;
            set
            {
                _restrictions = value;
                OnPropertyChanged();
            }
        }

        [NotNull] private List<Guid> _installBefore = new List<Guid>();
        [NotNull] public List<Guid> InstallBefore
        {
            get => _installBefore;
            set
            {
                _installBefore = value;
                OnPropertyChanged();
            }
        }

        [NotNull] private List<Guid> _installAfter = new List<Guid>();
        [NotNull] public List<Guid> InstallAfter
        {
            get => _installAfter;
            set
            {
                _installAfter = value;
                OnPropertyChanged();
            }
        }

        private bool _nonEnglishFunctionality;
        public bool NonEnglishFunctionality
        {
            get => _nonEnglishFunctionality;
            set
            {
                _nonEnglishFunctionality = value;
                OnPropertyChanged();
            }
        }

        [NotNull] private string _installationMethod = string.Empty;
        [NotNull] public string InstallationMethod
        {
            get => _installationMethod;
            set
            {
                _installationMethod = value;
                OnPropertyChanged();
            }
        }

        [NotNull] [ItemNotNull] private ObservableCollection<Instruction> _instructions = new ObservableCollection<Instruction>();
        [NotNull] [ItemNotNull] public ObservableCollection<Instruction> Instructions
        {
            get => _instructions;
            set
            {
                if (_instructions != value)
                {
                    _instructions = value;
                    OnPropertyChanged();
                }
            }
        }

        [NotNull] private ObservableCollection<Option> _options = new ObservableCollection<Option>();
        [NotNull] public ObservableCollection<Option> Options
        {
            get => _options;
            set
            {
                if (_options != value)
                {
                    _options.CollectionChanged -= CollectionChanged;

                    _options = value;

                    _options.CollectionChanged += CollectionChanged;

                    OnPropertyChanged();
                }
            }
        }

        [NotNull][ItemNotNull] private List<string> _language = new List<string>();
        [NotNull][ItemNotNull] public List<string> Language
        {
            get => _language;
            set
            {
                _language = value;
                OnPropertyChanged();
            }
        }

        [NotNull] private List<string> _modLink = new List<string>();
        [NotNull] public List<string> ModLink
        {
            get => _modLink;
            set
            {
                _modLink = value;
                OnPropertyChanged();
            }
        }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        [NotNull]
        public string SerializeComponent()
        {
            var serializedComponentDict = (Dictionary<string, object>)Serializer.SerializeObject( this );
            if ( serializedComponentDict is null )
                throw new NullReferenceException(nameof( serializedComponentDict ));

            CollectionUtils.RemoveEmptyCollections( serializedComponentDict );
            StringBuilder tomlString = FixSerializedTomlDict( serializedComponentDict );

            var rootTable = new Dictionary<string, object>( StringComparer.OrdinalIgnoreCase )
            {
                {
                    "thisMod", serializedComponentDict
                },
            };

            _ = tomlString
                .Insert(
                    index: 0,
                    Toml.FromModel( rootTable )
                        .Replace( oldValue: "[thisMod]", newValue: "[[thisMod]]" )
                ).ToString();
            return string.IsNullOrWhiteSpace( tomlString.ToString() )
                ? throw new InvalidOperationException( message: "Could not serialize into a valid tomlin string" )
                : Serializer.FixWhitespaceIssues( tomlString.ToString() );
        }

        private static StringBuilder FixSerializedTomlDict(
            [NotNull] Dictionary<string, object> serializedComponentDict,
            [CanBeNull] StringBuilder tomlString = null,
            [CanBeNull] List<string> nestedKeys = null
        )
        {
            if (serializedComponentDict is null)
                throw new ArgumentNullException(nameof(serializedComponentDict));

            if (tomlString is null)
                tomlString = new StringBuilder();

            // not the cleanest solution, but it works.
            var keysCopy = serializedComponentDict.Keys.ToList();
            foreach (string key in keysCopy)
            {
                object value = serializedComponentDict[key];
                if (!(value is List<dynamic> propertyList))
                    continue;

                Type listEntriesType = propertyList.GetType().GetGenericArguments()[0];
                if (!listEntriesType.IsClass || listEntriesType == typeof(string))
                    continue;

                bool found = false;
                foreach (object classInstanceObj in propertyList)
                {
                    if (classInstanceObj == null)
                        continue;

                    if (classInstanceObj is string)
                        break;

                    found = true;

                    var model = new Dictionary<string, object>
                    {
                        {
                            "thisMod", new Dictionary<string, object>
                            {
                                {
                                    key, classInstanceObj
                                },
                            }
                        },
                    };
                    _ = tomlString.AppendLine(
                        Toml.FromModel(
                            model
                        ).Replace(
                            $"thisMod.{key}",
                            $"[thisMod.{key}]"
                        )
                    );

                    /*if (classInstanceObj is Dictionary<string, object> anotherDict)
                    {
                        // Call the method recursively to process the nested dictionary
                        FixSerializedTomlDict(anotherDict, tomlString);
                    }*/
                }

                if (found)
                    _ = serializedComponentDict.Remove( key );
            }

            return tomlString;
        }


        private void DeserializeComponent( [NotNull] IDictionary<string, object> componentDict )
        {
            if ( !( componentDict is TomlTable ) )
                throw new ArgumentException( "[TomlError] Expected a TOML table for component data." );

            Name = GetRequiredValue<string>( componentDict, key: "Name" );
            _ = Logger.LogAsync( $"{Environment.NewLine}== Deserialize next component '{Name}' ==" );
            Guid = GetRequiredValue<Guid>( componentDict, key: "Guid" );
            Description = GetValueOrDefault<string>( componentDict, key: "Description" ) ?? string.Empty;
            Directions = GetValueOrDefault<string>( componentDict, key: "Directions" ) ?? string.Empty;
            Category = GetValueOrDefault<string>( componentDict, key: "Category" ) ?? string.Empty;
            Tier = GetValueOrDefault<string>( componentDict, key: "Tier" ) ?? string.Empty;
            Language = GetValueOrDefault<List<string>>( componentDict, key: "Language" ) ?? new List<string>();
            Author = GetValueOrDefault<string>( componentDict, key: "Author" ) ?? string.Empty;
            Dependencies = GetValueOrDefault<List<Guid>>( componentDict, key: "Dependencies" ) ?? new List<Guid>();
            Restrictions = GetValueOrDefault<List<Guid>>( componentDict, key: "Restrictions" ) ?? new List<Guid>();
            InstallBefore = GetValueOrDefault<List<Guid>>( componentDict, key: "InstallBefore" ) ?? new List<Guid>();
            InstallAfter = GetValueOrDefault<List<Guid>>( componentDict, key: "InstallAfter" ) ?? new List<Guid>();

            ModLink = GetValueOrDefault<List<string>>( componentDict, key: "ModLink" ) ?? new List<string>();
            if ( ModLink.IsNullOrEmptyCollection() )
            {
                string modLink = GetValueOrDefault<string>( componentDict, key: "ModLink" ) ?? string.Empty;
                if ( string.IsNullOrEmpty( modLink ) )
                {
                    Logger.LogError( "Could not deserialize key 'ModLink'" );
                }
                else
                {
                    ModLink = modLink.Split( new[] { "\r\n", "\n" }, StringSplitOptions.None ).ToList();
                }
            }

            IsSelected = GetValueOrDefault<bool>( componentDict, key: "IsSelected" );

            Instructions =
                DeserializeInstructions( GetValueOrDefault<IList<object>>( componentDict, key: "Instructions" ) );
            Instructions.ForEach( instruction => instruction?.SetParentComponent( this ) );
            Options = DeserializeOptions( GetValueOrDefault<IList<object>>( componentDict, key: "Options" ) );

            // Validate and log additional errors/warnings.
            _ = Logger.LogAsync( $"Successfully deserialized component '{Name}'" );
        }

        public static void OutputConfigFile(
            [ItemNotNull][NotNull] IEnumerable<Component> components,
            [NotNull] string filePath
        )
        {
            if ( components is null )
                throw new ArgumentNullException( nameof( components ) );
            if ( filePath is null )
                throw new ArgumentNullException( nameof( filePath ) );

            var stringBuilder = new StringBuilder();

            foreach ( Component thisComponent in components )
            {
                _ = stringBuilder.AppendLine( thisComponent.SerializeComponent() );
            }

            string tomlinString = stringBuilder.ToString();
            File.WriteAllText( new InsensitivePath(filePath).FullName, tomlinString );
        }

        [NotNull]
        public static string GenerateModDocumentation( [NotNull][ItemNotNull] List<Component> componentsList )
        {
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

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
                _ = string.Equals( component.Language.FirstOrDefault(), b: "All", StringComparison.OrdinalIgnoreCase )
                    ? sb.AppendLine( "**Supported Languages**: ALL" )
                    : sb.AppendLine( "**Supported Languages**: [" )
                        .AppendLine(
                            string.Join(
                                $",{Environment.NewLine}",
                                component.Language.Select( item => $"{indentation}{item}" )
                            )
                        )
                        .Append( ']' )
                        .AppendLine();

                _ = sb.Append( "**Directions**: " )
                    .AppendLine( component.Directions );

                // Instructions
                _ = sb.AppendLine();
                _ = sb.AppendLine( "**Installation Instructions:**" );
                foreach ( Instruction instruction in component.Instructions )
                {
                    if ( instruction.Action == Instruction.ActionType.Extract )
                        continue;

                    _ = sb.Append( "**Action**: " )
                        .AppendLine( instruction.ActionString );
                    if ( instruction.Action == Instruction.ActionType.Move )
                    {
                        _ = sb.Append( "**Overwrite existing files?**: " )
                            .AppendLine(
                                instruction.Overwrite
                                    ? "NO"
                                    : "YES"
                            );
                    }

                    string thisLine =
                        $"Source: [{Environment.NewLine}{string.Join( $",{Environment.NewLine}", instruction.Source.Select( item => $"{indentation}{item}" ) )}{Environment.NewLine}]";

                    if ( instruction.Action != Instruction.ActionType.Move )
                    {
                        thisLine = thisLine.Replace( oldValue: "Source: ", newValue: "" );
                    }

                    _ = sb.AppendLine( thisLine );

                    if ( !string.IsNullOrEmpty( instruction.Destination ) && instruction.Action == Instruction.ActionType.Move )
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
        private ObservableCollection<Instruction> DeserializeInstructions(
            [CanBeNull][ItemCanBeNull] IList<object> instructionsSerializedList
        )
        {
            if ( instructionsSerializedList.IsNullOrEmptyCollection() )
            {
                _ = Logger.LogWarningAsync( $"No instructions found for component '{Name}'" );
                return new ObservableCollection<Instruction>();
            }

            var instructions = new ObservableCollection<Instruction>();

            // ReSharper disable once PossibleNullReferenceException
            for ( int index = 0; index < instructionsSerializedList.Count; index++ )
            {
                Dictionary<string, object> instructionDict =
                    Serializer.SerializeIntoDictionary( instructionsSerializedList[index] );

                Serializer.DeserializePathInDictionary( instructionDict, key: "Source" );
                Serializer.DeserializeGuidDictionary( instructionDict, key: "Restrictions" );
                Serializer.DeserializeGuidDictionary( instructionDict, key: "Dependencies" );

                var instruction = new Instruction();
                string strAction = GetValueOrDefault<string>( instructionDict, key: "Action" );
                if ( Enum.TryParse( strAction, ignoreCase: true, out Instruction.ActionType action ) )
                {
                    instruction.Action = action;
                    _ = Logger.LogAsync(
                        $"{Environment.NewLine} -- Deserialize instruction #{index + 1} action '{action}'"
                    );
                }
                else
                {
                    _ = Logger.LogErrorAsync( $"{Environment.NewLine} -- Missing/invalid action for instruction #{index}" );
                    instruction.Action = Instruction.ActionType.Unset;
                }

                instruction.Arguments = GetValueOrDefault<string>( instructionDict, key: "Arguments" ) ?? string.Empty;
                instruction.Overwrite = GetValueOrDefault<bool>( instructionDict, key: "Overwrite" );

                instruction.Restrictions
                    = GetValueOrDefault<List<Guid>>( instructionDict, key: "Restrictions" ) ?? new List<Guid>();
                instruction.Dependencies
                    = GetValueOrDefault<List<Guid>>( instructionDict, key: "Dependencies" ) ?? new List<Guid>();
                instruction.Source = GetValueOrDefault<List<string>>( instructionDict, key: "Source" )
                    ?? new List<string>();
                instruction.Destination =
                    GetValueOrDefault<string>( instructionDict, key: "Destination" ) ?? string.Empty;
                instructions.Add( instruction );
            }

            return instructions;
        }

        [ItemNotNull]
        [NotNull]
        private ObservableCollection<Option> DeserializeOptions(
            [CanBeNull][ItemCanBeNull] IList<object> optionsSerializedList
        )
        {
            if ( optionsSerializedList.IsNullOrEmptyCollection() )
            {
                _ = Logger.LogVerboseAsync( $"No options found for component '{Name}'" );
                return new ObservableCollection<Option>();
            }

            var options = new ObservableCollection<Option>();

            // ReSharper disable once PossibleNullReferenceException
            for ( int index = 0; index < optionsSerializedList.Count; index++ )
            {
                var optionsDict = (IDictionary<string, object>)optionsSerializedList[index];
                if ( optionsDict is null )
                    continue;

                Serializer.DeserializeGuidDictionary( optionsDict, key: "Restrictions" );
                Serializer.DeserializeGuidDictionary( optionsDict, key: "Dependencies" );

                var option = new Option();
                _ = Logger.LogAsync(
                    $"{Environment.NewLine}-- Deserialize option #{index + 1}"
                );

                option.Name = GetRequiredValue<string>( optionsDict, key: "Name" );
                option.Description = GetValueOrDefault<string>( optionsDict, key: "Description" ) ?? string.Empty;
                _ = Logger.LogAsync( $"{Environment.NewLine}== Deserialize next option '{Name}' ==" );
                option.Guid = GetRequiredValue<Guid>( optionsDict, key: "Guid" );
                option.Restrictions
                    = GetValueOrDefault<List<Guid>>( optionsDict, key: "Restrictions" ) ?? new List<Guid>();
                option.Dependencies
                    = GetValueOrDefault<List<Guid>>( optionsDict, key: "Dependencies" ) ?? new List<Guid>();
                option.Instructions = DeserializeInstructions(
                    GetValueOrDefault<IList<object>>( optionsDict, key: "Instructions" ) );
                option.IsSelected = GetValueOrDefault<bool>( optionsDict, key: "IsSelected" );
                options.Add( option );
            }

            return options;
        }

        [NotNull]
        private static T GetRequiredValue<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key )
        {
            T value = GetValue<T>( dict, key, required: true );
            // ReSharper disable once CompareNonConstrainedGenericWithNull
            return value == null
                ? throw new InvalidOperationException( "GetValue cannot return null for a required value." )
                : value;
        }

        [CanBeNull]
        private static T GetValueOrDefault<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key ) =>
            GetValue<T>( dict, key, required: false );

        // why did I do this...
        /// <summary>
        /// The function `GetValue` is a generic method that retrieves a value from a dictionary and performs
        /// type conversion if necessary.
        /// </summary>
        /// <param name="dict">A dictionary that maps string keys to object values. This dictionary contains the
        /// data from which the value will be retrieved.</param>
        /// <param name="key">The `key` parameter is a string that represents the key of the value to retrieve
        /// from the dictionary.</param>
        /// <param name="required">The `required` parameter is a boolean flag that indicates whether the value
        /// for the specified key is required or not. If `required` is set to `true` and the value is missing or
        /// invalid, an exception will be thrown. If `required` is set to `false`, a default value</param>
        /// <returns>
        /// The method `GetValue{T}` returns a value of type `T`.
        /// </returns>
        [CanBeNull]
        private static T GetValue<T>( [NotNull] IDictionary<string, object> dict, [NotNull] string key, bool required )
        {
            try
            {
                if ( dict is null )
                    throw new ArgumentNullException( nameof( dict ) );
                if ( key is null )
                    throw new ArgumentNullException( nameof( key ) );

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
                    case string valueStr:
                        if ( string.IsNullOrEmpty( valueStr ) )
                        {
                            return required
                                ? throw new KeyNotFoundException( $"'{key}' field cannot be empty." )
                                : default( T );
                        }

                        if ( targetType == typeof( Guid ) )
                        {
                            string guidStr = Serializer.FixGuidString( valueStr );
                            return !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out Guid guid )
                                ? (T)(object)guid
                                : required
                                    ? throw new ArgumentException( $"'{key}' field is not a valid Guid!" )
                                    : (T)(object)Guid.Empty;
                        }

                        if ( targetType == typeof( string ) )
                            return (T)(valueStr as object);

                        break;
                }

                Type genericListDefinition =
                    targetType.IsGenericType
                        ? targetType.GetGenericTypeDefinition()
                        : null;
                if ( genericListDefinition == typeof( List<> ) || genericListDefinition == typeof( IList<> ) )
                {
                    Type[] genericArgs = typeof( T ).GetGenericArguments();
                    Type listElementType = genericArgs.Length > 0
                        ? genericArgs[0]
                        : typeof( string );
                    Type listType = typeof( List<> ).MakeGenericType( listElementType );

                    var list = (T)Activator.CreateInstance( listType );
                    MethodInfo addMethod = list?.GetType().GetMethod( name: "Add" );

                    if ( value is IEnumerable<object> enumerableValue )
                    {
                        foreach ( object item in enumerableValue )
                        {
                            if ( listElementType == typeof( Guid )
                                && Guid.TryParse(
                                    item?.ToString(),
                                    out Guid guidItem )
                                )
                            {
                                addMethod?.Invoke( list, new[] { (object)guidItem } );
                            }
                            else
                            {
                                addMethod?.Invoke( list, new[] { item } );
                            }
                        }
                    }
                    else
                    {
                        _ = addMethod?.Invoke( list, new[] { value } );
                    }

                    return list;
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
            catch ( RuntimeBinderException ) when ( !required )
            {
                return default;
            }
            catch ( InvalidCastException ) when ( !required )
            {
                return default;
            }

            return default;
        }

        [CanBeNull]
        public static Component DeserializeTomlComponent( [NotNull] string tomlString )
        {
            if ( tomlString is null )
                throw new ArgumentNullException( nameof( tomlString ) );

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

                    Logger.Log( message.Message );
                }

                return null;
            }

            // Get the array of Component tables
            IDictionary<string, object> tomlTable = tomlDocument.ToModel();

            IList<TomlTable> componentTableThing = new List<TomlTable>();
            switch ( tomlTable["thisMod"] )
            {
                case TomlArray componentTable:
                    componentTableThing.Add( (TomlTable)componentTable[0] );
                    break;
                case TomlTableArray componentTables:
                    componentTableThing = componentTables;
                    break;
            }

            // Deserialize each IDictionary<string, object> into a Component object
            var component = new Component();
            foreach ( TomlTable tomlComponent in componentTableThing )
            {
                if ( tomlComponent is IDictionary<string, object> componentDict )
                    component.DeserializeComponent( componentDict );
            }

            return component;
        }

        [NotNull]
        [ItemNotNull]
        public static List<Component> ReadComponentsFromFile( [NotNull] string filePath )
        {
            if ( filePath is null )
                throw new ArgumentNullException( nameof( filePath ) );

            try
            {
                // Read the contents of the file into a string
                string tomlString = File.ReadAllText( new InsensitivePath(filePath).FullName )
                    // the code expects instructions to always be defined. When it's not, code errors and prevents a save.
                    // make the user experience better by just removing the empty instructions key.
                    .Replace( oldValue: "Instructions = []", string.Empty )
                    .Replace( oldValue: "Options = []", string.Empty );

                if ( string.IsNullOrWhiteSpace( tomlString ) )
                {
                    throw new InvalidDataException(
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
                var componentTables = (IList<TomlTable>)tomlTable[key: "thisMod"];

                // Deserialize each IDictionary<string, object> into a Component object
                var components = new List<Component>();
                if ( componentTables is null )
                    return components;

                foreach ( TomlTable tomlComponent in componentTables )
                {
                    if ( tomlComponent is null )
                        continue;

                    var thisComponent = new Component();
                    thisComponent.DeserializeComponent( tomlComponent );

                    components.Add( thisComponent );
                }

                return components;
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, customMessage: "There was a problem serializing the components in the file." );
                throw;
            }
        }

        public enum InstallExitCode
        {
            [Description( "Completed Successfully" )]
            Success,

            [Description( "A dependency or restriction violation between components has occurred." )]
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
            ValidationPostInstallMismatch,
        }

        public async Task<InstallExitCode> InstallAsync( [NotNull] List<Component> componentsList )
        {
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            try
            {
                (InstallExitCode, Dictionary<SHA1, FileInfo>) result = await ExecuteInstructionsAsync(
                    Instructions,
                    componentsList
                );
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
            [NotNull][ItemNotNull] IList<Instruction> theseInstructions,
            [NotNull][ItemNotNull] List<Component> componentsList
        )
        {
            if ( theseInstructions is null )
                throw new ArgumentNullException( nameof( theseInstructions ) );
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            if ( !ShouldInstallComponent( componentsList ) )
            {
                return ( InstallExitCode.DependencyViolation, null );
            }

            InstallExitCode installExitCode = InstallExitCode.Success;

            for ( int instructionIndex = 1; instructionIndex <= theseInstructions.Count; instructionIndex++ )
            {
                int index = instructionIndex;
                Instruction instruction = theseInstructions[instructionIndex - 1];

                if ( !ShouldRunInstruction( instruction, componentsList ) )
                    continue;

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
                switch ( instruction.Action )
                {
                    case Instruction.ActionType.Extract:
                        instruction.SetRealPaths();
                        exitCode = await instruction.ExtractFileAsync();
                        break;
                    case Instruction.ActionType.Delete:
                        instruction.SetRealPaths( true );
                        exitCode = instruction.DeleteFile();
                        break;
                    case Instruction.ActionType.DelDuplicate:
                        instruction.SetRealPaths( true );
                        instruction.DeleteDuplicateFile(caseInsensitive: true);
                        exitCode = Instruction.ActionExitCode.Success;
                        break;
                    case Instruction.ActionType.Copy:
                        instruction.SetRealPaths();
                        exitCode = await instruction.CopyFileAsync();
                        break;
                    case Instruction.ActionType.Move:
                        instruction.SetRealPaths();
                        exitCode = await instruction.MoveFileAsync();
                        break;
                    case Instruction.ActionType.Rename:
                        instruction.SetRealPaths(true);
                        exitCode = instruction.RenameFile();
                        break;
                    case Instruction.ActionType.HoloPatcher:
                    case Instruction.ActionType.TSLPatcher:
                        instruction.SetRealPaths();
                        exitCode = await instruction.ExecuteTSLPatcherAsync();
                        break;
                    case Instruction.ActionType.Execute:
                    case Instruction.ActionType.Run:
                        instruction.SetRealPaths(true);
                        exitCode = await instruction.ExecuteProgramAsync();
                        break;
                    case Instruction.ActionType.Choose:
                        instruction.SetRealPaths(true);

                        List<Option> chosenOptions = instruction.GetChosenOptions();
                        foreach ( Option thisOption in chosenOptions )
                        {
                            ( installExitCode, _ ) = await ExecuteInstructionsAsync(
                                thisOption.Instructions,
                                componentsList
                            );
                        }

                        break;
                    /*case "confirm":
                    (var sourcePaths, var something) = instruction.ParsePaths();
                bool confirmationResult = await confirmDialog.ShowConfirmationDialog(sourcePaths.FirstOrDefault());
                if (!confirmationResult)
                {
                    this.Confirmations.Add(true);
                }
                break;*/
                    default:
                        // Handle unknown instruction type here
                        await Logger.LogWarningAsync( $"Unknown instruction '{instruction.ActionString}'" );
                        exitCode = Instruction.ActionExitCode.UnknownInstruction;
                        break;
                }

                _ = Logger.LogVerboseAsync(
                    $"Instruction #{instructionIndex} '{instruction.ActionString}' exited with code {exitCode}"
                );
                if ( exitCode != Instruction.ActionExitCode.Success )
                {
                    await Logger.LogErrorAsync(
                        $"FAILED Instruction #{instructionIndex} Action '{instruction.ActionString}'"
                    );
                    bool? confirmationResult = await PromptUserInstallError(
                        $"An error occurred during the installation of '{Name}':"
                        + Environment.NewLine
                        + Utility.Utility.GetEnumDescription( exitCode )
                    );

                    switch ( confirmationResult )
                    {
                        // repeat instruction
                        case true:
                            instructionIndex--;
                            continue;

                        // execute next instruction
                        case false:
                            continue;

                        // case null: cancel installing this mod (user closed confirmation dialog)
                        default:
                            return ( InstallExitCode.UserCancelledInstall, null );
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

                async Task<bool?> PromptUserInstallError( string message ) =>
                    await CallbackObjects.ConfirmCallback.ShowConfirmationDialog(
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

            return ( installExitCode, new Dictionary<SHA1, FileInfo>() );
        }

        [NotNull]
        public static Dictionary<string, List<Component>> GetConflictingComponents(
            [NotNull] List<Guid> dependencyGuids,
            [NotNull] List<Guid> restrictionGuids,
            [NotNull][ItemNotNull] List<Component> componentsList
        )
        {
            if ( dependencyGuids is null )
                throw new ArgumentNullException( nameof( dependencyGuids ) );
            if ( restrictionGuids is null )
                throw new ArgumentNullException( nameof( restrictionGuids ) );
            if ( componentsList == null )
                throw new ArgumentNullException( nameof( componentsList ) );

            var conflicts = new Dictionary<string, List<Component>>();
            if ( dependencyGuids.Count > 0 )
            {
                var dependencyConflicts = new List<Component>();

                foreach (Guid requiredGuid in dependencyGuids)
                {
                    Component checkComponent = FindComponentFromGuid(requiredGuid, componentsList);
                    if ( checkComponent == null )
                    {
                        // sometimes a component will be defined later by a user and all they have defined is a guid.
                        // this hacky solution will support that syntax.
                        // Only needed for 'dependencies' not 'restrictions'.
                        var componentGuidNotFound = new Component
                        {
                            Name = "Component Undefined with GUID.", Guid = requiredGuid,
                        };
                        dependencyConflicts.Add( componentGuidNotFound );
                    }
                    else if ( !checkComponent.IsSelected )
                    {
                        dependencyConflicts.Add(checkComponent);
                    }
                }


                if ( dependencyConflicts.Count > 0 )
                    conflicts["Dependency"] = dependencyConflicts;
            }

            // ReSharper disable once InvertIf
            if ( restrictionGuids.Count > 0 )
            {
                var restrictionConflicts = restrictionGuids.Select(
                        requiredGuid => FindComponentFromGuid( requiredGuid, componentsList ) )
                    .Where( checkComponent => checkComponent != null && checkComponent.IsSelected
                ).ToList();

                if ( restrictionConflicts.Count > 0 )
                    conflicts["Restriction"] = restrictionConflicts;
            }

            return conflicts;
        }

        //The component will be installed if all of the following conditions are met:
        //The component has no dependencies or restrictions.
        //The component has dependencies, and all of the required components are being installed.
        //The component has restrictions, but none of the restricted components are being installed.
        public bool ShouldInstallComponent(
            [NotNull][ItemNotNull] List<Component> componentsList
        )
        {
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            Dictionary<string, List<Component>> conflicts = GetConflictingComponents(
                Dependencies,
                Restrictions,
                componentsList
            );
            return conflicts.Count == 0;
        }

        //The instruction will run if any of the following conditions are met:
        //The instruction has no dependencies or restrictions.
        //The instruction has dependencies, and all of the required components are being installed.
        //The instruction has restrictions, but none of the restricted components are being installed.
        public static bool ShouldRunInstruction(
            [NotNull] Instruction instruction,
            [NotNull] List<Component> componentsList
        )
        {
            if ( instruction is null )
                throw new ArgumentNullException( nameof( instruction ) );
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            Dictionary<string, List<Component>> conflicts = GetConflictingComponents(
                instruction.Dependencies,
                instruction.Restrictions,
                componentsList
            );
            return conflicts.Count == 0;
        }

        [CanBeNull]
        public static Component FindComponentFromGuid(
            Guid guidToFind,
            [NotNull][ItemNotNull] List<Component> componentsList
        )
        {
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

            Component foundComponent = null;
            foreach ( Component component in componentsList )
            {
                if ( component.Guid == guidToFind )
                {
                    foundComponent = component;
                    break;
                }

                foreach ( Option thisOption in component.Options )
                {
                    if ( thisOption.Guid == guidToFind )
                    {
                        foundComponent = thisOption;
                        break;
                    }
                }

                if ( foundComponent != null )
                    break;
            }

            return foundComponent;
        }

        [NotNull]
        public static List<Component> FindComponentsFromGuidList(
            [NotNull] List<Guid> guidsToFind,
            [NotNull] List<Component> componentsList
        )
        {
            if ( guidsToFind is null )
                throw new ArgumentNullException( nameof( guidsToFind ) );
            if ( componentsList is null )
                throw new ArgumentNullException( nameof( componentsList ) );

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
            if ( components is null )
                throw new ArgumentNullException( nameof( components ) );

            Dictionary<Guid, GraphNode> nodeMap = CreateDependencyGraph( components );

            var visitedNodes = new HashSet<GraphNode>();
            var orderedComponents = new List<Component>();

            foreach ( GraphNode node in nodeMap.Values )
            {
                if ( visitedNodes.Contains( node ) )
                    continue;

                DepthFirstSearch( node, visitedNodes, orderedComponents );
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
            if ( node is null )
                throw new ArgumentNullException( nameof( node ) );
            if ( visitedNodes is null )
                throw new ArgumentNullException( nameof( visitedNodes ) );
            if ( orderedComponents is null )
                throw new ArgumentNullException( nameof( orderedComponents ) );

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
            if ( Instructions.IsNullOrEmptyOrAllNull() )
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
        public void DeleteOption( int index ) => Options.RemoveAt( index );

        public void MoveInstructionToIndex( [NotNull] Instruction thisInstruction, int index )
        {
            if ( thisInstruction is null || index < 0 || index >= Instructions.Count )
                throw new ArgumentException( "Invalid instruction or index." );

            int currentIndex = Instructions.IndexOf( thisInstruction );
            if ( currentIndex < 0 )
                throw new ArgumentException( "Instruction does not exist in the list." );

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

        public void CreateOption( int index = 0 )
        {
            var option = new Option
            {
                Name = Path.GetFileNameWithoutExtension( Path.GetTempFileName() ),
                Guid = Guid.NewGuid(),
            };
            if ( Instructions.IsNullOrEmptyOrAllNull() )
            {
                if ( index != 0 )
                {
                    Logger.LogError( "Cannot create option at index when list is empty." );
                    return;
                }

                Options.Add( option );
            }
            else
            {
                Options.Insert( index, option );
            }
        }

        public void MoveOptionToIndex( [NotNull] Option thisOption, int index )
        {
            if ( thisOption is null || index < 0 || index >= Options.Count )
            {
                throw new ArgumentException( "Invalid option or index." );
            }

            int currentIndex = Options.IndexOf( thisOption );
            if ( currentIndex < 0 )
            {
                throw new ArgumentException( "Option does not exist in the list." );
            }

            if ( index == currentIndex )
            {
                _ = Logger.LogAsync(
                    $"Cannot move Option '{thisOption.Name}' from {currentIndex} to {index}. Reason: Indices are the same."
                );
                return;
            }

            Options.RemoveAt( currentIndex );
            Options.Insert( index, thisOption );

            _ = Logger.LogVerboseAsync(
                $"Option '{thisOption.Name}' moved from {currentIndex} to {index}"
            );
        }


        // used for the ui.
        public event PropertyChangedEventHandler PropertyChanged;
        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged();
        private void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
