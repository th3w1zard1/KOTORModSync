// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.CallbackDialogs;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Component = KOTORModSync.Core.Component;

// ReSharper disable UnusedParameter.Local
// ReSharper disable MemberCanBeMadeStatic.Local
// ReSharper disable AsyncVoidMethod

namespace KOTORModSync
{
    public partial class MainWindow : Window
    {
        private Component _currentComponent;
        private bool _installRunning;

        private string _originalContent;
        private Window _outputWindow;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize the logger
            Logger.Initialize();

            // Create callback objects for use with KOTORModSync.Core
            CallbackObjects.SetCallbackObjects(
                new ConfirmationDialogCallback( this ),
                new OptionsDialogCallback( this )
            );
        }

        private MainConfig MainConfigInstance { get; set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );

            MainGrid = this.FindControl<Grid>( "MainGrid" ) ?? throw new NullReferenceException( "MainGrid undefined in MainWindow." );
            if ( MainGrid.ColumnDefinitions?.Count != 3 ) throw new NullReferenceException( "MainGrid incorrectly defined, expected 3 columns." );

            ColumnDefinition componentListColumn = MainGrid.ColumnDefinitions[0] ?? throw new NullReferenceException( "Column 0 of MainGrid (component list column) not defined." );
            ColumnDefinition configColumn = MainGrid.ColumnDefinitions[2] ?? throw new NullReferenceException( "Column 2 of MainGrid (component list column) not defined." );


            // Column 0
            componentListColumn.Width = new GridLength( 250 );
            LeftTreeView = this.FindControl<TreeView>( "LeftTreeView" );
            ApplyEditorButton = this.FindControl<Button>( "ApplyEditorButton" );
            // Column 1
            ComponentsItemsControl = this.FindControl<ItemsControl>( "ComponentsItemsControl" );
            TabControl = this.FindControl<TabControl>( "TabControl" );
            InitialTab = this.FindControl<TabItem>( "InitialTab" );
            GuiEditTabItem = this.FindControl<TabItem>( "GuiEditTabItem" );
            RawEditTabItem = this.FindControl<TabItem>( "RawEditTabItem" );
            RawEditTextBox = this.FindControl<TextBox>( "RawEditTextBox" );
            if ( RawEditTextBox == null )
            {
                throw new NullReferenceException( "RawEditTextBox not defined for MainWindow." );
            }

            RawEditTextBox.LostFocus
                += RawEditTextBox_LostFocus; // Prevents RawEditTextBox from being cleared when clicking elsewhere(?)
            RawEditTextBox.DataContext = new ObservableCollection<string>();

            // Column 3
            configColumn.Width = new GridLength( 250 );
            MainConfigInstance = new MainConfig();
            MainConfigStackPanel = this.FindControl<StackPanel>( "MainConfigStackPanel" ) ?? throw new NullReferenceException( "MainConfigStackPanel not defined for MainWindow." );

            MainConfigStackPanel.DataContext = MainConfigInstance;
        }

        // test the options dialog for use with the 'Options' TomlTable.
        public async void Testwindow()
        {
            // Create an instance of OptionsDialogCallback
            var optionsDialogCallback = new OptionsDialogCallback( this );

            // Create a list of options
            var options = new List<string> { "Option 1", "Option 2", "Option 3" };

            // Show the options dialog and get the selected option
            string selectedOption = await optionsDialogCallback.ShowOptionsDialog( options );

            // Use the selected option
            if ( selectedOption != null )
            {
                // Option selected, do something with it
                Console.WriteLine( "Selected option: " + selectedOption );
            }
            else
            {
                // No option selected or dialog closed
                Console.WriteLine( "No option selected or dialog closed" );
            }
        }

        [ItemCanBeNull]
        private async Task<string> OpenFile()
        {
            try
            {
                var filters = new List<FileDialogFilter>( 10 )
                {
                    new FileDialogFilter { Name = "Mod Sync File", Extensions = { "toml", "tml" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                };

                string[] result = await ShowFileDialog( false, filters );
                if ( result?.Length > 0 )
                {
                    return result[0]; // Retrieve the first selected file path
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        [ItemCanBeNull]
        private async Task<List<string>> OpenFiles()
        {
            try
            {
                var filters = new List<FileDialogFilter>( 10 )
                {
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                };

                string[] filePaths = await ShowFileDialog( false, filters, true );
                if ( filePaths is null )
                {
                    await Logger.LogVerboseAsync( "User did not select any files." );
                    return null;
                }

                await Logger.LogAsync( $"Selected files: [{string.Join( $",{Environment.NewLine}", filePaths )}]" );
                return filePaths.ToList();
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                return null;
            }
        }

        [ItemCanBeNull]
        private async Task<string> OpenFolder()
        {
            try
            {
                string[] thisFolder = await ShowFileDialog( true, null );
                return thisFolder?[0];
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                return null;
            }
        }


        [ItemCanBeNull]
        private async Task<string> SaveFile( [CanBeNull] List<string> defaultExt = null )
        {
            try
            {
                if ( defaultExt is null )
                {
                    defaultExt = new List<string> { "toml", "tml" };
                }

                var dialog = new SaveFileDialog
                {
                    DefaultExtension = defaultExt.FirstOrDefault(),
                    Filters =
                    {
                        new FileDialogFilter { Name = "All Files", Extensions = { "*" } },
                        new FileDialogFilter { Name = "Preferred Extensions", Extensions = defaultExt }
                    }
                };

                // Show the dialog and wait for a result.
                if ( VisualRoot is Window parent )
                {
                    string filePath = await dialog.ShowAsync( parent );
                    if ( !string.IsNullOrEmpty( filePath ) )
                    {
                        await Logger.LogAsync( $"Selected file: {filePath}" );
                        return filePath;
                    }
                }
                else
                {
                    throw new InvalidOperationException( "Could not open dialog - parent window not found" );
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }


        [ItemCanBeNull]
        private async Task<string[]> ShowFileDialog(
            bool isFolderDialog,
            [CanBeNull] List<FileDialogFilter> filters,
            bool allowMultiple = false
        )
        {
            try
            {
                if ( !( VisualRoot is Window parent ) )
                {
                    await Logger.LogAsync(
                        $"Could not open {( isFolderDialog ? "folder" : "file" )} dialog - parent window not found"
                    );
                    return default;
                }

                string[] results = isFolderDialog
                    ? new[] { await new OpenFolderDialog().ShowAsync( parent ) }
                    : await new OpenFileDialog { AllowMultiple = allowMultiple, Filters = filters }.ShowAsync( parent );

                if ( results is null
                    || results.Length == 0 )
                {
                    await Logger.LogVerboseAsync( "User did not make a selection" );
                    return default;
                }

                await Logger.LogAsync(
                    $"Selected {( isFolderDialog ? "folder" : "file" )}: {string.Join( ", ", results )}"
                );
                return results;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task<bool> FindDuplicateComponents( [NotNull] List<Component> components )
        {
            if ( components == null )
            {
                throw new ArgumentNullException( nameof( components ) );
            }

            // Check for duplicate GUID
            bool duplicatesFixed = true;
            foreach ( Component component in components )
            {
                Component duplicateComponent
                    = components.FirstOrDefault( c => c.Guid == component.Guid && c != component );

                if ( duplicateComponent is null )
                {
                    continue;
                }

                if ( !Guid.TryParse( duplicateComponent.Guid.ToString(), out Guid _ ) )
                {
                    _ = Logger.LogWarningAsync(
                        $"Invalid GUID for component '{component.Name}'. Got '{component.Guid}'"
                    );

                    if ( MainConfig.AttemptFixes )
                    {
                        _ = Logger.LogVerboseAsync( "Fixing the above issue automatically..." );
                        duplicateComponent.Guid = Guid.NewGuid();
                    }
                }

                string message
                    = $"Component '{component.Name}' has a duplicate GUID with component '{duplicateComponent.Name}'";
                _ = Logger.LogAsync( message );

                bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        $"{message}.{Environment.NewLine}Assign a random GUID to '{duplicateComponent.Name}'? (default: NO)"
                    )
                    == true;

                if ( confirm == true )
                {
                    duplicateComponent.Guid = Guid.NewGuid();
                    _ = Logger.LogAsync( $"Replaced GUID of component '{duplicateComponent.Name}'" );
                }
                else
                {
                    _ = Logger.LogVerboseAsync(
                        $"User canceled GUID replacement for component '{duplicateComponent.Name}'"
                    );
                    duplicatesFixed = false;
                }
            }

            return duplicatesFixed;
        }

        private async void LoadInstallFile_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            // Open the file dialog to select a file
            try
            {
                string filePath = await OpenFile();
                if ( string.IsNullOrEmpty( filePath ) )
                {
                    return;
                }

                var thisFile = new FileInfo( filePath );

                // Verify the file type
                string fileExtension = thisFile.Extension;
                if ( !new List<string> { ".toml", ".tml", ".txt" }.Contains(
                        fileExtension,
                        StringComparer.OrdinalIgnoreCase
                    ) )
                {
                    _ = Logger.LogAsync( $"Invalid extension for file '{thisFile.Name}'" );
                    return;
                }

                if ( MainConfig.AllComponents.Count > 0 )
                {
                    bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        "You already have a config loaded." + " Do you want to load this instruction file anyway?"
                    );
                    if ( confirm != true )
                    {
                        return;
                    }
                }

                // Load components dynamically
                MainConfigInstance.allComponents = Component.ReadComponentsFromFile( filePath );
                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        public async void LoadMarkdown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                string filePath = await OpenFile();
                if ( string.IsNullOrEmpty( filePath ) )
                {
                    return; // user cancelled
                }

                using ( var reader = new StreamReader( filePath ) )
                {
                    string fileContents = await reader.ReadToEndAsync();
                    if ( MainConfig.AllComponents?.Count > 0
                        && await ConfirmationDialog.ShowConfirmationDialog(
                            this,
                            "You already have a config loaded. Do you want to load the markdown anyway?"
                        )
                        != true )
                    {
                        return;
                    }

                    MainConfigInstance.allComponents = ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) );
                    await ProcessComponentsAsync( MainConfig.AllComponents );
                }
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void BrowseSourceFiles_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                var button = (Button)sender;
                // Get the item's data context based on the clicked button
                Instruction thisInstruction = (Instruction)button.DataContext
                    ?? throw new NullReferenceException( "Could not find instruction instance" );

                // Open the file dialog to select a file
                List<string> files = await OpenFiles();
                if ( files is null )
                {
                    _ = Logger.LogVerboseAsync(
                        "No files chosen in BrowseSourceFiles_Click, returning to previous values"
                    );
                    return;
                }

                if ( files.Any( string.IsNullOrEmpty ) )
                {
                    throw new ArgumentOutOfRangeException(
                        nameof( files ),
                        $"Invalid files found. Please report this issue to the developer: [{string.Join( ",", files )}]"
                    );
                }

                // Replace path with prefixed variables.
                for ( int i = 0; i < files.Count; i++ )
                {
                    string filePath = files[i];
                    files[i] = MainConfig.SourcePath != null
                        ? Utility.RestoreCustomVariables( filePath )
                        : filePath;
                }

                if ( MainConfig.SourcePath is null )
                {
                    _ = Logger.LogWarningAsync(
                        "Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set prior."
                    );
                }

                thisInstruction.Source = files;

                // refresh the text box
                if ( button.Tag is TextBox sourceTextBox )
                {
                    string convertedItems = new Converters.ListToStringConverter().Convert(
                        files,
                        typeof( string ),
                        null,
                        CultureInfo.CurrentCulture
                    ) as string;

                    sourceTextBox.Text = convertedItems;
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void BrowseDestination_Click( [CanBeNull] object sender, [CanBeNull] RoutedEventArgs e )
        {
            try
            {
                Button button = (Button)sender ?? throw new InvalidOperationException();
                Instruction thisInstruction = (Instruction)button.DataContext
                    ?? throw new NullReferenceException( "Could not find instruction instance" );

                // Open the file dialog to select a file
                string filePath = await OpenFolder();
                if ( filePath is null )
                {
                    _ = Logger.LogVerboseAsync(
                        "No file chosen in BrowseDestination_Click."
                        + $" Will continue using '{thisInstruction.Destination}'"
                    );
                    return;
                }

                if ( MainConfig.SourcePath is null )
                {
                    _ = Logger.LogAsync(
                        "Directories not set, setting raw folder path without custom variable <<kotorDirectory>>"
                    );
                    thisInstruction.Destination = filePath;
                    return;
                }

                thisInstruction.Destination = Utility.RestoreCustomVariables( filePath );

                // refresh the text box
                if ( button.Tag is TextBox destinationTextBox )
                {
                    destinationTextBox.Text = thisInstruction.Destination;
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void GenerateGuidButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                _currentComponent.Guid = Guid.NewGuid();
                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private async void SaveButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "Please select a component from the list or create a new one before saving."
                    );
                    return;
                }

                await Logger.LogVerboseAsync( $"Selected {_currentComponent.Name}" );

                if ( !CheckForChanges() )
                {
                    await Logger.LogVerboseAsync( "No changes detected, ergo nothing to save." );
                    return;
                }

                bool? confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(
                    this,
                    "Are you sure you want to save?"
                );
                if ( confirmationResult != true )
                {
                    return;
                }

                (bool success, string output) = await SaveChanges();
                if ( !success )
                {
                    await InformationDialog.ShowInformationDialog( this, output );
                }

                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async Task<(bool success, string informationMessage)> PreinstallValidation()
        {
            try
            {
                if ( MainConfigInstance is null
                    || MainConfig.DestinationPath is null
                    || MainConfig.SourcePath is null )
                {
                    return (false, "Please set your directories first");
                }

                if ( MainConfig.AllComponents.Count == 0 )
                {
                    return (false,
                        "No instructions loaded! Press 'Load Instructions File' or create some instructions first.");
                }

                await Logger.LogAsync( "Checking for duplicate components..." );
                bool noDuplicateComponents = await FindDuplicateComponents( MainConfig.AllComponents );

                // Ensure necessary directories are writable.
                await Logger.LogAsync( "Ensuring both the mod directory and the install directory are writable..." );
                bool isInstallDirectoryWritable = Utility.IsDirectoryWritable( MainConfig.DestinationPath );
                bool isModDirectoryWritable = Utility.IsDirectoryWritable( MainConfig.SourcePath );

                await Logger.LogAsync( "Validating the order of operations and install order of all components..." );
                (bool isCorrectOrder, List<Component> reorderedList)
                    = Component.ConfirmComponentsInstallOrder( MainConfig.AllComponents );
                if ( !isCorrectOrder
                    && MainConfig.AttemptFixes )
                {
                    await Logger.LogWarningAsync( "Incorrect order detected, but has been automatically reordered." );
                    MainConfigInstance.allComponents = reorderedList;
                    isCorrectOrder = true;
                }

                await Logger.LogAsync( "Validating individual components, this might take a while..." );
                bool individuallyValidated = true;
                foreach ( Component component in MainConfig.AllComponents )
                {
                    if ( !component.IsSelected )
                    {
                        continue;
                    }


                    // Confirm that dependencies are all found in InstallBefore and InstallAfter keys:
                    bool installOrderKeysDefined = component.Dependencies?.All(
                            item => component.InstallBefore?.Contains( item ) == true
                                || component.InstallAfter?.Contains( item ) == true
                        )
                        == true
                        || component.Dependencies is null;

                    if ( component.Restrictions?.Count > 0
                        && component.IsSelected )
                    {
                        List<Component> restrictedComponentsList
                            = Component.FindComponentsFromGuidList( component.Restrictions, MainConfig.AllComponents );
                        foreach ( Component restrictedComponent in restrictedComponentsList )
                        {
                            if ( restrictedComponent.IsSelected )
                            {
                                await Logger.LogErrorAsync(
                                    $"Cannot install '{component.Name}' due to '{restrictedComponent.Name}' being selected for install."
                                );
                                individuallyValidated = false;
                            }
                        }
                    }

                    if ( component.Dependencies?.Count > 0
                        && component.IsSelected )
                    {
                        List<Component> dependencyComponentsList
                            = Component.FindComponentsFromGuidList( component.Dependencies, MainConfig.AllComponents );
                        foreach ( Component dependencyComponent in dependencyComponentsList )
                        {
                            if ( !dependencyComponent.IsSelected )
                            {
                                await Logger.LogErrorAsync(
                                    $"Cannot install '{component.Name}' due to '{dependencyComponent.Name}' not being selected for install."
                                );
                                individuallyValidated = false;
                            }
                        }
                    }

                    if ( !installOrderKeysDefined )
                    {
                        await Logger.LogErrorAsync(
                            $"'{component.Name}' 'InstallBefore' and 'InstallAfter' keys must be defined for all dependencies."
                        );
                        individuallyValidated = false;
                    }

                    var validator = new ComponentValidation( component, MainConfig.AllComponents );
                    await Logger.LogVerboseAsync( $" == Validating '{component.Name}' == " );
                    individuallyValidated &= validator.Run();
                }

                await Logger.LogVerboseAsync( "Finished validating all components." );

                string informationMessage = string.Empty;
                if ( !isCorrectOrder )
                {
                    informationMessage = "Your components are not in the correct order."
                        + " There are specific mods found that need to be installed either before or after another or more mods."
                        + " Please ensure the correct order, or rerun the validator with 'Attempt Fixes' enabled.";
                    await Logger.LogErrorAsync( informationMessage );
                }

                if ( !isInstallDirectoryWritable )
                {
                    informationMessage = "The Install directory is not writable!"
                        + " Please ensure administrative privileges or reinstall KOTOR"
                        + " to a directory with write access.";
                    await Logger.LogErrorAsync( informationMessage );
                }

                if ( !isModDirectoryWritable )
                {
                    informationMessage = "The Mod directory is not writable!"
                        + " Please ensure administrative privileges or choose a new mod directory.";
                    await Logger.LogErrorAsync( informationMessage );
                }

                if ( !noDuplicateComponents )
                {
                    informationMessage = "There were several duplicate components found."
                        + " Please ensure all components are unique and none have conflicting GUIDs.";
                    await Logger.LogErrorAsync( informationMessage );
                }

                if ( !individuallyValidated )
                {
                    informationMessage = "Some components failed to individually validate.";
                    await Logger.LogErrorAsync( informationMessage );
                }

                if ( informationMessage.Equals( string.Empty ) )
                {
                    return (true,
                        "No issues found. If you encounter any problems during the installation, please contact the developer.");
                }

                return (false, informationMessage);
            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
                return (false, "Unknown error, check the output window for more information.");
            }
        }

        private async void ValidateButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                (bool success, string informationMessage) = await PreinstallValidation();
                await InformationDialog.ShowInformationDialog( this, informationMessage );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void AddComponentButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            // Create a new default component with a new GUID
            try
            {
                Component newComponent
                    = Component.DeserializeTomlComponent( Component.DefaultComponent + Instruction.DefaultInstructions )
                    ?? throw new NullReferenceException( "Could not deserialize default template" );

                newComponent.Guid = Guid.NewGuid();
                newComponent.Name = "new mod_" + Path.GetFileNameWithoutExtension( Path.GetRandomFileName() );
                // Add the new component to the collection
                MainConfigInstance.allComponents.Add( newComponent );
                _currentComponent = newComponent;

                // Load into the editor ( optional )
                LoadComponentDetails( newComponent );

                // Refresh the TreeView to reflect the changes
                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void RefreshComponents_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => await ProcessComponentsAsync( MainConfig.AllComponents );

        private async void RemoveComponentButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            // Get the selected component from the TreeView
            try
            {
                if ( _currentComponent is null )
                {
                    Logger.Log( "No component loaded into editor - nothing to remove." );
                    return;
                }

                // todo:
                if ( MainConfig.AllComponents.Any( c => c.Dependencies?.Any( g => g == _currentComponent.Guid ) == true ) )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        $"Cannot remove '{_currentComponent.Name}', there are several components that rely on it. Please address this problem first."
                    );
                    return;
                }

                // Remove the selected component from the collection
                _ = MainConfigInstance.allComponents.Remove( _currentComponent );
                _currentComponent = null;

                // Refresh the TreeView to reflect the changes
                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void SetDirectories_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                await InformationDialog.ShowInformationDialog(
                    this,
                    "Please select your KOTOR(2) directory. (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")"
                );
                string chosenFolder = await OpenFolder();
                if ( chosenFolder != null )
                {
                    var kotorInstallDir = new DirectoryInfo( chosenFolder );
                    MainConfigInstance.destinationPath = kotorInstallDir;
                }

                await InformationDialog.ShowInformationDialog(
                    this,
                    "Please select your mod directory (where ALL your mods are downloaded)."
                );
                chosenFolder = await OpenFolder();
                if ( chosenFolder is null )
                {
                    _ = Logger.LogVerboseAsync( "User cancelled selecting folder" );
                    return;
                }

                var modDirectory = new DirectoryInfo( chosenFolder );
                MainConfigInstance.sourcePath = modDirectory;
            }
            catch ( ArgumentNullException )
            {
                await Logger.LogVerboseAsync( "User cancelled selecting folder" );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex, "Unknown error - please report to a developer" );
            }
        }

        private async void InstallModSingle_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "There's already another installation running, please check the output window."
                    );
                    return;
                }

                if ( MainConfigInstance is null
                    || MainConfig.DestinationPath is null )
                {
                    var informationDialog = new InformationDialog { InfoText = "Please set your directories first" };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                if ( _currentComponent is null )
                {
                    var informationDialog = new InformationDialog
                    {
                        InfoText = "Please choose a mod to install from the left list first"
                    };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                string name = _currentComponent.Name; // use correct name even if user clicks another component.
                if ( name is null )
                {
                    throw new NullReferenceException( "Component does not have a valid 'Name' field." );
                }

                bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                    this,
                    _currentComponent.Directions
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Press Yes to execute the provided directions now."
                );
                if ( confirm != true )
                {
                    await Logger.LogAsync( $"User cancelled install of '{name}'" );
                    return;
                }



                var validator = new ComponentValidation( _currentComponent, MainConfig.AllComponents );
                await Logger.LogVerboseAsync( $" == Validating '{name}' == " );
                if ( !validator.Run() )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "This component could not be validated, please check the output window."
                    );
                }

                try
                {
                    _installRunning = true;

                    Component.InstallExitCode exitCode = await Task.Run(
                        () => _currentComponent.InstallAsync( MainConfig.AllComponents )
                    );
                    _installRunning = false;

                    if ( exitCode != 0 )
                    {
                        await InformationDialog.ShowInformationDialog(
                            this,
                            $"There was a problem installing '{name}':"
                            + Environment.NewLine
                            + Utility.GetEnumDescription( exitCode )
                            + Environment.NewLine
                            + Environment.NewLine
                            + " Check the output window for details."
                        );
                    }
                    else
                    {
                        await Logger.LogAsync( $"Successfully installed '{name}'" );
                    }
                }
                catch ( Exception )
                {
                    _installRunning = false;
                    throw;
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private bool _progressWindowClosed;

        private async void StartInstall_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "There's already an installation running, please check the output window."
                    );
                    return;
                }

                (bool success, string informationMessage) = await PreinstallValidation();
                if ( !success )
                {
                    await InformationDialog.ShowInformationDialog( this, informationMessage );
                    return;
                }

                if ( await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        "WARNING! While there is code in place to prevent incorrect instructions from running,"
                        + $" the program cannot predict every possible mistake a user could make in a config file.{Environment.NewLine}"
                        + " Additionally, the modbuild can be 20GB or larger! As a result, we cannot create any backups."
                        + " Please ensure you've backed up your Install directory"
                        + $" and you've ensured you're running a Vanilla installation.{Environment.NewLine}{Environment.NewLine}"
                        + " Are you sure you're ready to continue?"
                    )
                    != true )
                {
                    return;
                }

                if ( await ConfirmationDialog.ShowConfirmationDialog( this, "Really install all mods?" ) != true )
                {
                    return;
                }


                try
                {
                    _ = Logger.LogAsync( "Start installing all mods..." );
                    _installRunning = true;

                    var progressWindow = new ProgressWindow();
                    progressWindow.Closed += ProgressWindowClosed;
                    progressWindow.ProgressBar.Value = 0;
                    progressWindow.Show();
                    _progressWindowClosed = false;

                    for ( int index = 0; index < MainConfig.AllComponents.Count; index++ )
                    {
                        if ( _progressWindowClosed )
                        {
                            _installRunning = false;
                            _ = Logger.LogAsync( "User cancelled install by closing the progress window." );
                            return;
                        }

                        Component component = MainConfig.AllComponents[index];
                        await Dispatcher.UIThread.InvokeAsync(
                            async () =>
                            {
                                progressWindow.ProgressTextBlock.Text = $"Installing '{component.Name}'..."
                                    + Environment.NewLine
                                    + Environment.NewLine
                                    + "Executing the provided directions..."
                                    + Environment.NewLine
                                    + Environment.NewLine
                                    + component.Directions;

                                double percentComplete = (double)index / MainConfig.AllComponents.Count;
                                progressWindow.ProgressBar.Value = percentComplete;
                                progressWindow.InstalledRemaining.Text
                                    = $"{index}/{MainConfig.AllComponents.Count + 1} Components Installed";
                                progressWindow.PercentCompleted.Text = $"{Math.Round( percentComplete * 100 )}%";


                                // Additional fallback options
                                await Task.Delay( 100 ); // Introduce a small delay
                                await Dispatcher.UIThread.InvokeAsync(
                                    () => { }
                                ); // Invoke an empty action to ensure UI updates are processed
                                await Task.Delay( 50 ); // Introduce another small delay
                            }
                        );

                        // Ensure the UI updates are processed
                        await Task.Yield();
                        await Task.Delay( 200 );

                        if ( !component.IsSelected )
                        {
                            await Logger.LogAsync( $"Skipping install of '{component.Name}' (unchecked)" );
                            continue;
                        }

                        await Logger.LogAsync( $"Start Install of '{component.Name}'..." );
                        Component.InstallExitCode exitCode = await component.InstallAsync( MainConfig.AllComponents );
                        await Logger.LogAsync( $"Install of '{component.Name}' finished with exit code {exitCode}" );

                        if ( exitCode != 0 )
                        {
                            bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                                this,
                                $"There was a problem installing '{component.Name}':"
                                + Environment.NewLine
                                + Utility.GetEnumDescription( exitCode )
                                + Environment.NewLine
                                + Environment.NewLine
                                + " Check the output window for details."
                                + Environment.NewLine
                                + Environment.NewLine
                                + $"Skip '{component.Name}' and install the next mod anyway? (NOT RECOMMENDED!)"
                            );
                            if ( confirm == true )
                            {
                                continue;
                            }

                            await Logger.LogAsync( "Install cancelled" );
                            break;
                        }

                        await Logger.LogAsync( $"Finished installed '{component.Name}'" );
                    }

                    progressWindow.Close();
                    _installRunning = false;
                }
                catch ( Exception )
                {
                    _installRunning = false;
                    throw;
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void ProgressWindowClosed( [CanBeNull] object sender, [CanBeNull] EventArgs e )
        {
            if ( !( sender is ProgressWindow progressWindow ) )
            {
                return;
            }

            progressWindow.ProgressBar.Value = 0;
            progressWindow.Closed -= ProgressWindowClosed;
            progressWindow.Dispose();
            _progressWindowClosed = true;
        }

        private async void DocsButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                string file = await SaveFile( new List<string>( 65535 ) { "txt" } );
                if ( file is null )
                {
                    return;
                }

                string docs = Component.GenerateModDocumentation( MainConfig.AllComponents );
                await SaveDocsToFileAsync( file, docs );
                string message = $"Saved documentation of {MainConfig.AllComponents.Count} mods to '{file}'";
                await InformationDialog.ShowInformationDialog( this, message );
                _ = Logger.LogAsync( message );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex, "Error generating and saving documentation" );
                await InformationDialog.ShowInformationDialog(
                    this,
                    "An unexpected error occurred while generating and saving documentation."
                );
            }
        }

        private static async Task SaveDocsToFileAsync( [CanBeNull] string filePath, [CanBeNull] string documentation )
        {
            try
            {
                if ( !string.IsNullOrEmpty( documentation ) )
                {
                    using ( var writer = new StreamWriter( filePath ) )
                    {
                        await writer.WriteAsync( documentation );
                        await writer.FlushAsync();
                        // ReSharper disable once MethodHasAsyncOverload
                        // not available in net462
                        writer.Dispose();
                    }
                }
            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
            }
        }

        private void TabControl_SelectionChanged( [CanBeNull] object sender, [CanBeNull] SelectionChangedEventArgs e )
        {
            if ( !( ( sender as TabControl )?.SelectedItem is TabItem selectedItem ) )
            {
                return;
            }

            if ( selectedItem.Header is null )
            {
                return;
            }

            // Don't show content of any tabs (except the hidden one) if there's no content.
            if ( MainConfig.AllComponents.Count == 0
                || LeftTreeView.SelectedItem is null )
            {
                TabControl.SelectedItem = InitialTab;
                return;
            }

            switch ( selectedItem.Header.ToString() )
            {
                // Show/hide the appropriate content based on the selected tab
                case "Raw Edit":
                    RawEditTextBox.IsVisible = true;
                    ApplyEditorButton.IsVisible = true;
                    break;
                case "GUI Edit":
                    RawEditTextBox.IsVisible = false;
                    ApplyEditorButton.IsVisible = false;
                    break;
            }
        }

        private async void LoadComponentDetails( [CanBeNull] Component selectedComponent, bool confirmation = true )
        {
            try
            {
                if ( selectedComponent is null
                    || RawEditTextBox is null )
                {
                    return;
                }

                // todo: figure out what we're doing with _originalComponent
                _ = Logger.LogVerboseAsync( $"Loading {selectedComponent.Name}..." );

                _originalContent = selectedComponent.SerializeComponent();
                if ( _originalContent.Equals( RawEditTextBox.Text, StringComparison.Ordinal )
                    && !string.IsNullOrWhiteSpace( RawEditTextBox.Text )
                    && selectedComponent != _currentComponent )
                {
                    bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        "You're attempting to load the component, but there may be unsaved changes still in the editor. Really continue?"
                    );

                    // double check with user before overwrite
                    if ( confirmation && confirmResult != true )
                    {
                        return;
                    }
                }

                // default to GuiEditTabItem.
                if ( InitialTab.IsSelected
                    || TabControl.SelectedIndex == int.MaxValue )
                {
                    TabControl.SelectedItem = GuiEditTabItem;
                }

                // populate raw editor
                RawEditTextBox.Text = _originalContent;
                // this tracks the currently selected component.
                _currentComponent = selectedComponent;
                // bind the selected component to the gui editor
                ComponentsItemsControl.Items = new ObservableCollection<Component> { selectedComponent };
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void RawEditTextBox_LostFocus
            ( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => e.Handled = true;

        private bool CheckForChanges()
        {
            string currentContent = RawEditTextBox.Text;
            return !string.Equals( currentContent, _originalContent );
        }

        private async Task<(bool, string Message)> SaveChanges()
        {
            try
            {
                // Get the selected component from the tree view
                if ( _currentComponent is null )
                {
                    return (false,
                        "TreeViewItem does not correspond to a valid Component"
                        + Environment.NewLine
                        + "Please report this issue to a developer, this should never happen.");
                }

                var newComponent = Component.DeserializeTomlComponent( RawEditTextBox.Text );

                // Find the corresponding component in the collection
                int index = MainConfig.AllComponents.IndexOf( _currentComponent );
                // if not selected, find the index of the _currentComponent.
                if ( index < 0
                    || index >= MainConfig.AllComponents.Count )
                {
                    index = MainConfig.AllComponents.FindIndex( c => c.Equals( _currentComponent ) );
                }

                if ( index < 0
                    && _currentComponent is null )
                {
                    string componentName = string.IsNullOrWhiteSpace( newComponent?.Name )
                        ? "."
                        : $" '{newComponent.Name}'.";
                    string errorMessage = $"Could not find the index of component{componentName}"
                        + " Ensure you single-clicked on a component on the left before pressing save."
                        + " Please back up your work and try again.";

                    return (false, errorMessage);
                }

                // Update the properties of the component
                MainConfigInstance.allComponents[index] = newComponent
                    ?? throw new InvalidDataException(
                        "Could not deserialize raw text into a Component instance in memory."
                    );

                await ProcessComponentsAsync( MainConfig.AllComponents ); // Refresh the tree view to reflect the changes
                return (
                    true,
                    $"Saved {newComponent.Name} successfully. Refer to the output window for more information."
                );
            }
            catch ( InvalidDataException ex )
            {
                return (false, ex.Message + Environment.NewLine + "Refer to the output window for details.");
            }
            catch ( Exception ex )
            {
                const string customMessage = "An unexpected exception was thrown. Please report this to the developer.";
                Logger.LogException( ex, customMessage );
                return (false, customMessage + Environment.NewLine + "Refer to the output window for details.");
            }
        }

        private async void MoveComponentListItem( [CanBeNull] Control selectedTreeViewItem, int relativeIndex )
        {
            try
            {
                var treeViewComponent = (Component)selectedTreeViewItem.Tag;

                int index = MainConfig.AllComponents.IndexOf( treeViewComponent );
                if ( treeViewComponent is null
                    || ( index == 0 && relativeIndex < 0 )
                    || index == -1
                    || index + relativeIndex == MainConfig.AllComponents.Count )
                {
                    return;
                }

                _ = MainConfig.AllComponents.Remove( treeViewComponent );
                MainConfigInstance.allComponents.Insert( index + relativeIndex, treeViewComponent );
                await ProcessComponentsAsync( MainConfig.AllComponents );
                await Logger.LogVerboseAsync(
                    $"Moved '{treeViewComponent.Name}' to index #{MainConfig.AllComponents.IndexOf( treeViewComponent ) + 1}"
                );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void MoveUpButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
                    || !( selectedTreeViewItem.Parent is ItemsControl parentItemsControl ) )
                {
                    return;
                }

                MoveComponentListItem( selectedTreeViewItem, -1 );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private void MoveDownButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
                    || !( selectedTreeViewItem.Parent is ItemsControl parentItemsControl ) )
                {
                    return;
                }

                MoveComponentListItem( selectedTreeViewItem, 1 );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private async void SaveModFile_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                string filePath = await SaveFile();
                if ( filePath is null )
                {
                    return;
                }

                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
                if ( rootItem is null )
                {
                    return;
                }

                string randomFileName = Path.GetFileNameWithoutExtension( Path.GetRandomFileName() );
                Logger.Log( $"Creating backup mod config at {filePath}" );

                using ( var writer = new StreamWriter( filePath ) )
                {
                    foreach ( Component c in MainConfig.AllComponents )
                    {
                        string tomlContents = c.SerializeComponent();
                        await writer.WriteLineAsync( tomlContents );
                    }
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        public void ComponentCheckboxChecked( [CanBeNull] Component component, [CanBeNull] HashSet<Component> visitedComponents, bool suppressErrors = false )
        {
            try
            {
                // Check if the component has already been visited
                if ( visitedComponents.Contains( component ) )
                {
                    // Conflicting component that cannot be resolved automatically
                    if ( !suppressErrors )
                    {
                        Logger.LogError(
                            $"Component '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!"
                        );
                    }
                }

                // Add the component to the visited set
                _ = visitedComponents.Add( component );

                Dictionary<string, List<Component>> conflicts = Component.GetConflictingComponents(
                    component.Dependencies,
                    component.Restrictions,
                    MainConfig.AllComponents
                );

                // Handling conflicts based on what's defined for THIS component
                if ( conflicts.TryGetValue( "Dependency", out List<Component> dependencyConflicts ) )
                {
                    foreach ( Component conflictComponent in dependencyConflicts )
                    {
                        if ( conflictComponent.IsSelected == false )
                        {
                            conflictComponent.IsSelected = true;
                            ComponentCheckboxChecked( conflictComponent, visitedComponents );
                        }
                    }
                }

                if ( conflicts.TryGetValue( "Restriction", out List<Component> restrictionConflicts ) )
                {
                    foreach ( Component conflictComponent in restrictionConflicts )
                    {
                        if ( conflictComponent.IsSelected )
                        {
                            conflictComponent.IsSelected = false;
                            ComponentCheckboxUnchecked( conflictComponent, visitedComponents );
                        }
                    }
                }

                // Handling OTHER component's defined restrictions based on the change to THIS component.
                foreach ( Component c in MainConfig.AllComponents )
                {
                    if ( !c.IsSelected || c.Restrictions?.Contains( component.Guid ) != true )
                    {
                        continue;
                    }

                    c.IsSelected = false;
                    ComponentCheckboxUnchecked( c, visitedComponents );
                }
            }
            catch ( Exception e )
            {
                Logger.LogException( e );
            }
        }

        public void ComponentCheckboxUnchecked( [CanBeNull] Component component, [CanBeNull] HashSet<Component> visitedComponents, bool suppressErrors = false )
        {
            try
            {
                // Check if the component has already been visited
                if ( visitedComponents.Contains( component ) )
                {
                    // Conflicting component that cannot be resolved automatically
                    if ( !suppressErrors )
                    {
                        Logger.LogError(
                            $"Component '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!"
                        );
                    }
                }

                // handle root item's checkbox
                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
                if ( rootItem != null )
                {
                    var headerPanel = rootItem.Header as DockPanel;
                    CheckBox checkBox = headerPanel?.Children.OfType<CheckBox>().FirstOrDefault();

                    if ( checkBox != null
                        && !suppressErrors )
                    {
                        checkBox.IsChecked = null;
                    }
                }

                // Add the component to the visited set
                _ = visitedComponents.Add( component );

                Dictionary<string, List<Component>> conflicts = Component.GetConflictingComponents(
                    component.Dependencies,
                    component.Restrictions,
                    MainConfig.AllComponents
                );

                // Handling OTHER component's defined dependencies based on the change to THIS component.
                foreach ( Component c in MainConfig.AllComponents )
                {
                    if ( c.IsSelected
                        && c.Dependencies?.Contains( component.Guid ) == true )
                    {
                        c.IsSelected = false;
                        ComponentCheckboxUnchecked( c, visitedComponents );
                    }
                }
            }
            catch ( Exception e )
            {
                Logger.LogException( e );
            }
        }

        private CheckBox CreateComponentCheckbox( [CanBeNull] Component component )
        {
            var checkBox = new CheckBox { Name = "IsSelected", IsChecked = true };
            var binding = new Binding( "IsSelected" ) { Source = component, Mode = BindingMode.TwoWay };

            // Set up the event handler for the checkbox
            checkBox.Checked += ( sender, e ) => ComponentCheckboxChecked( component, new HashSet<Component>() );
            checkBox.Unchecked += ( sender, e ) => ComponentCheckboxUnchecked( component, new HashSet<Component>() );

            if ( ToggleButton.IsCheckedProperty != null )
            {
                _ = checkBox.Bind( ToggleButton.IsCheckedProperty, binding );
            }

            return checkBox;
        }

        private Control CreateComponentHeader( Component component )
        {
            CheckBox checkBox = CreateComponentCheckbox( component );
            var header = new DockPanel();
            header.Children.Add( checkBox );
            header.Children.Add( new TextBlock { Text = component.Name } );

            return header;
        }

        public ICommand ItemClickCommand => new RelayCommand(
            parameter =>
            {
                if ( !( parameter is Component component ) )
                {
                    return;
                }

                LoadComponentDetails( component );
            }
        );

        private TreeViewItem CreateComponentItem( [CanBeNull] Component component )
        {
            var componentItem = new TreeViewItem
            {
                Header = CreateComponentHeader( component ),
                Tag = component,
                IsExpanded = true
            };

            componentItem.Tapped += ( sender, e ) =>
            {
                ItemClickCommand.Execute( component );
                e.Handled = true; // Prevent event bubbling
            };

            return componentItem;
        }

        [CanBeNull]
        private static TreeViewItem FindExistingItem( ItemsControl parentItem, [CanBeNull] Component component )
        {
            foreach ( object item in parentItem.Items )
            {
                if ( !( item is TreeViewItem treeViewItem ) )
                {
                    continue;
                }

                if ( treeViewItem.Tag is Component treeViewComponent
                    && treeViewComponent.Equals( component ) )
                {
                    return treeViewItem;
                }
            }

            return null;
        }

        [CanBeNull]
        private static Component GetComponentFromGuid( List<Component> componentsList, Guid guid ) =>
            componentsList.Find( c => c.Guid == guid );

        private void CreateDependencyItems( [NotNull] Component component, [NotNull] ItemsControl parentItem )
        {
            if ( component?.Dependencies is null
                || component.Dependencies.Count == 0 )
            {
                return;
            }

            foreach ( Guid dependencyGuid in component.Dependencies )
            {
                Component dependency = GetComponentFromGuid( MainConfig.AllComponents, dependencyGuid );
                if ( dependency is null )
                {
                    continue;
                }

                CreateTreeViewItem( dependency, parentItem );
            }
        }


        private void CreateTreeViewItem( [NotNull] Component component, [NotNull] ItemsControl parentItem )
        {
            try
            {
                if ( parentItem is null )
                {
                    throw new ArgumentNullException( nameof( parentItem ) );
                }

                if ( component is null )
                {
                    throw new ArgumentNullException( nameof( component ) );
                }

                if ( !( parentItem.Items is AvaloniaList<object> parentItemItems ) )
                {
                    throw new InvalidCastException(
                        "parentItem must have a non-nullable Items property and be of type AvaloniaList<object>."
                    );
                }

                TreeViewItem existingItem = FindExistingItem( parentItem, component );

                if ( existingItem != null )
                {
                    existingItem.Tag = component;
                    return;
                }

                // Remove the TreeViewItem from the top level LeftTreeView if it needs to be nested as a dependency.
                if ( parentItem.Parent is ItemsControl parentParentItem )
                {
                    var parentParentItems = (AvaloniaList<object>)parentParentItem.Items;
                    TreeViewItem secondToTopLevelItem = FindExistingItem( parentParentItem, component );

                    if ( secondToTopLevelItem != null )
                    {
                        _ = parentParentItems.Remove( secondToTopLevelItem );
                    }
                }

                TreeViewItem componentItem = CreateComponentItem( component );
                parentItemItems.Add( componentItem );

                CreateDependencyItems( component, componentItem );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, "Unexpected exception while creating tree view item" );
            }
        }

        private TreeViewItem CreateRootTreeViewItem()
        {
            var rootItem = new TreeViewItem { IsExpanded = true };

            var checkBox = new CheckBox { Name = "IsSelected", IsChecked = true };
            var binding = new Binding( "IsSelected" );

            // Set up the event handler for the checkbox
            bool manualSet = false;
            checkBox.Checked += ( sender, e ) =>
            {
                if ( manualSet )
                {
                    return;
                }

                bool allChecked = true;

                var finishedComponents = new HashSet<Component>();
                foreach ( Component component in MainConfig.AllComponents )
                {
                    component.IsSelected = true;
                    ComponentCheckboxChecked( component, finishedComponents, true );
                }

                foreach ( Component component in MainConfig.AllComponents )
                {
                    if ( !component.IsSelected )
                    {
                        allChecked = false;
                        break;
                    }
                }

                if ( allChecked )
                {
                    return;
                }

                manualSet = true;
                checkBox.IsChecked = null;
                manualSet = false;
            };
            checkBox.Unchecked += ( sender, e ) =>
            {
                var finishedComponents = new HashSet<Component>();
                foreach ( Component component in MainConfig.AllComponents )
                {
                    component.IsSelected = false;
                    ComponentCheckboxUnchecked( component, finishedComponents, true );
                }
            };

            var header = new DockPanel();
            header.Children.Add( checkBox );
            header.Children.Add( new TextBlock { Text = "Components" } );
            rootItem.Header = header;

            if ( ToggleButton.IsCheckedProperty != null )
            {
                _ = checkBox.Bind( ToggleButton.IsCheckedProperty, binding );
            }

            return rootItem;
        }

        private async Task ProcessComponentsAsync( [NotNull] List<Component> componentsList )
        {
            try
            {
                if ( !( componentsList.Count > 0 ) )
                {
                    return;
                }

                try
                {
                    (bool isCorrectOrder, List<Component> reorderedList)
                        = Component.ConfirmComponentsInstallOrder( MainConfig.AllComponents );
                    if ( !isCorrectOrder )
                    {
                        await Logger.LogVerboseAsync( "Reordered list to match dependency structure." );
                        MainConfig.AllComponents = reorderedList;
                    }
                }
                catch ( KeyNotFoundException )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "Cannot process order of components."
                        + " There are circular dependency conflicts that cannot be automatically resolved."
                        + " Please resolve these before attempting an installation."
                    );
                    return;
                }

                // Create the root item for the tree view
                TreeViewItem rootItem = CreateRootTreeViewItem();

                // Iterate over the components and create tree view items
                foreach ( Component component in componentsList )
                {
                    CreateTreeViewItem( component, rootItem );
                }

                // Set the root item as the single item of the tree view
                // Create a collection to hold the root item
                var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

                // Set the root item collection as the items source of the tree view
                LeftTreeView.Items = rootItemsCollection;

                // Expand the tree. Too lazy to figure out the proper way.
                IEnumerator treeEnumerator = LeftTreeView.Items.GetEnumerator();
                _ = treeEnumerator.MoveNext();
                LeftTreeView.ExpandSubTree( (TreeViewItem)treeEnumerator.Current );

                if ( componentsList.Count == 0 )
                {
                    TabControl.SelectedItem = InitialTab;
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void AddNewInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

                var addButton = (Button)sender;
                var thisInstruction = addButton.Tag as Instruction;
                var thisComponent = addButton.Tag as Component;
                if ( thisInstruction is null
                    && thisComponent is null )
                {
                    throw new NullReferenceException( "Cannot find instruction instance from button." );
                }

                _currentComponent.Instructions = _currentComponent.Instructions ?? new List<Instruction>(); //todo

                int index;
                if ( thisInstruction is null )
                {
                    thisInstruction = new Instruction();
                    index = _currentComponent.Instructions.Count;
                }
                else
                {
                    index = _currentComponent.Instructions.IndexOf( thisInstruction );
                }

                _currentComponent.CreateInstruction( index );
                if ( thisInstruction.Action != null )
                {
                    if ( _currentComponent.Name != null )
                    {
                        await Logger.LogVerboseAsync(
                            $"Component '{_currentComponent.Name}': Instruction '{thisInstruction.Action}' created at index #{index}"
                        );
                    }
                }

                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void DeleteInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = _currentComponent.Instructions.IndexOf( thisInstruction );

                _currentComponent.DeleteInstruction( index );
                await Logger.LogVerboseAsync(
                    $"Component '{_currentComponent.Name}': instruction '{thisInstruction.Action}' deleted at index #{index}"
                );

                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void MoveInstructionUp_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = _currentComponent.Instructions.IndexOf( thisInstruction );

                _currentComponent.MoveInstructionToIndex( thisInstruction, index - 1 );
                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void MoveInstructionDown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = _currentComponent.Instructions.IndexOf( thisInstruction );

                _currentComponent.MoveInstructionToIndex( thisInstruction, index + 1 );
                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        public class RelayCommand : ICommand
        {
            private readonly Func<object, bool> _canExecute;
            private readonly Action<object> _execute;

            public RelayCommand( [NotNull] Action<object> execute, [CanBeNull] Func<object, bool> canExecute = null )
            {
                _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
                _canExecute = canExecute;
            }

            [UsedImplicitly] public event EventHandler CanExecuteChanged;

            public bool CanExecute( object parameter ) => _canExecute == null || _canExecute( parameter );
            public void Execute( object parameter ) => _execute?.Invoke( parameter );
        }


        private void OpenOutputWindow_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            if ( _outputWindow != null
                && _outputWindow.IsVisible )
            {
                _outputWindow.Close();
            }

            _outputWindow = new OutputWindow();
            _outputWindow.Show();
        }

        private bool _initialize = true;

        private void StyleComboBox_SelectionChanged( [NotNull] object sender, [NotNull] SelectionChangedEventArgs e )
        {
            try
            {
                if ( _initialize )
                {
                    _initialize = false;
                    return;
                }

                var comboBox = (ComboBox)sender;
                var selectedItem = (ComboBoxItem)comboBox.SelectedItem;

                string stylePath = (string)selectedItem.Tag;

                Styles[0] = new Style();

                if ( stylePath.Equals( "default" ) )
                {
                    InvalidateArrange(); // force repaint of entire window.
                    InvalidateMeasure(); // force repaint of entire window.
                    InvalidateVisual(); // force repaint of entire window.
                    TraverseControls( this, (ISupportInitialize)sender );
                    return;
                }

                var styleUriPath = new Uri( "avares://KOTORModSync" + stylePath );

                // Apply the selected style dynamically
                Styles[0] = new StyleInclude( styleUriPath ) { Source = styleUriPath };
                InvalidateArrange(); // force repaint of entire window.
                InvalidateMeasure(); // force repaint of entire window.
                InvalidateVisual(); // force repaint of entire window.
                TraverseControls( this, (ISupportInitialize)sender );
            }
            catch ( Exception exception )
            {
                Logger.LogException( exception );
            }
        }

        private static void TraverseControls
            ( [NotNull] IControl control, [NotNull] ISupportInitialize styleControlComboBox )
        {
            if ( control is null )
            {
                throw new ArgumentNullException( nameof( control ) );
            }

            if ( control == styleControlComboBox )
            {
                return; // fixes a crash that can happen while spamming the combobox style options.
            }

            // Reload the style of the control
            control.ApplyTemplate();

            var logicalControl = control as ILogical;
            if ( logicalControl.LogicalChildren is null )
            {
                return;
            }

            // Traverse the child controls recursively
            logicalControl.LogicalChildren.OfType<IControl>()
                .ToList()
                .ForEach( childControl => TraverseControls( childControl, styleControlComboBox ) );
        }
    }
}
