// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Component = KOTORModSync.Core.Component;

// ReSharper disable UnusedParameter.Local
// ReSharper disable MemberCanBeMadeStatic.Local
// ReSharper disable AsyncVoidMethod

namespace KOTORModSync.GUI
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Component> _selectedComponents = new ObservableCollection<Component>();
        private List<Component> _components = new List<Component>();
        private Component _currentComponent;
        private bool _installRunning;

        private RelayCommand _itemClickCommand;
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
        public ICommand ItemClickCommand => _itemClickCommand ?? ( _itemClickCommand = new RelayCommand( ItemClick ) );

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

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );

            MainGrid = this.FindControl<Grid>( "MainGrid" );
            // Column 0
            LeftTreeView = this.FindControl<TreeView>( "LeftTreeView" );
            ApplyEditorButton = this.FindControl<Button>( "ApplyEditorButton" );
            // Column 1
            TabControl = this.FindControl<TabControl>( "TabControl" );
            InitialTab = this.FindControl<TabItem>( "InitialTab" );
            GuiEditTabItem = this.FindControl<TabItem>( "GuiEditTabItem" );
            RawEditTabItem = this.FindControl<TabItem>( "RawEditTabItem" );
            RawEditTextBox = this.FindControl<TextBox>( "RawEditTextBox" );
            RawEditTextBox.LostFocus
                += RawEditTextBox_LostFocus; // Prevents RawEditTextBox from being cleared when clicking elsewhere(?)
            RawEditTextBox.DataContext = new ObservableCollection<string>();
            // Column 3
            MainConfigInstance = new MainConfig();
            MainConfigStackPanel = this.FindControl<StackPanel>( "MainConfigStackPanel" );
            MainConfigStackPanel.DataContext = MainConfigInstance;
        }

        public static IControl Build( object data )
        {
            try
            {
                // Create a dictionary to keep track of child TreeViewItems
                var childItems = new Dictionary<string, TreeViewItem>( 10000 );
                if ( !( data is Component component ) )
                {
                    throw new InvalidCastException( "Data variable should always be a Component." );
                }

                // If no dependencies we can return here.
                if ( component.Dependencies == null || component.Dependencies.Count == 0 )
                {
                    return new TextBlock
                    {
                        Text = component.Name
                    }; // Use a TextBlock for components without dependencies
                }

                // Create a TreeViewItem for the component
                var treeViewItem = new TreeViewItem { Header = component.Name };

                // Check if the component has any dependencies
                foreach ( string dependency in component.Dependencies )
                {
                    if ( childItems.ContainsKey( dependency ) )
                    {
                        continue;
                    }

                    // Create a new child TreeViewItem for each unique dependency
                    var childItem = new TreeViewItem { Header = dependency };
                    childItems.Add( dependency, childItem );
                }

                // Add child TreeViewItems to the parent TreeViewItem
                var items = treeViewItem.Items as IList;
                foreach ( TreeViewItem childItem in childItems.Values )
                {
                    if ( childItem != null
                            ? items != null
                            : throw new ArgumentNullException( nameof( childItem ) )
                       )
                    {
                        _ = items.Add( childItem );
                    }
                }

                return treeViewItem;
            }
            catch ( Exception e )
            {
                Console.WriteLine( e );
                throw;
            }
        }

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
                if ( filePaths == null )
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
        private async Task<string> SaveFile( List<string> defaultExt = null )
        {
            try
            {
                if ( defaultExt == null )
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
        private async Task<string[]> ShowFileDialog
        (
            bool isFolderDialog,
            List<FileDialogFilter> filters,
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

                if ( results == null || results.Length == 0 )
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

        private async Task ProcessComponents( [CanBeNull] List<Component> components )
        {
            if ( !( components?.Count > 0 ) )
            {
                return;
            }

            // Create the root item for the tree view
            var rootItem = new TreeViewItem { Header = "Components" };

            int i = 0;
            foreach ( Component component in components )
            {
                CreateTreeViewItem( component, rootItem );

                // Check for duplicate GUID
                Component duplicateComponent
                    = components.FirstOrDefault( c => c.Guid == component.Guid && c != component );

                if ( duplicateComponent == null )
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

                bool? confirm = i >= 2
                    || await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        $"{message}.\r\nAssign a random GUID to '{duplicateComponent.Name}'? (default: NO)"
                    )
                    == true;

                if ( confirm == true )
                {
                    i++;
                    duplicateComponent.Guid = Guid.NewGuid();
                    _ = Logger.LogAsync( $"Replaced GUID of component '{duplicateComponent.Name}'" );
                }
                else
                {
                    _ = Logger.LogVerboseAsync(
                        $"User canceled GUID replacement for component '{duplicateComponent.Name}'"
                    );
                }
            }

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    // Create a collection to hold the root item
                    var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

                    // Set the root item collection as the items source of the tree view
                    LeftTreeView.Items = rootItemsCollection;

                    // Expand the tree. Too lazy to figure out the proper way.
                    IEnumerator treeEnumerator = LeftTreeView.Items.GetEnumerator();
                    _ = treeEnumerator.MoveNext();
                    LeftTreeView.ExpandSubTree( (TreeViewItem)treeEnumerator.Current );
                }
            );
        }

        private async void LoadInstallFile_Click( object sender, RoutedEventArgs e )
        {
            // Open the file dialog to select a file
            try
            {
                string filePath = await OpenFile();
                if ( string.IsNullOrEmpty( filePath ) )
                {
                    return;
                }

                // Verify the file type
                string fileExtension = Path.GetExtension( filePath );
                if ( !new List<string> { ".toml", ".tml", ".txt" }.Contains(
                        fileExtension,
                        StringComparer.OrdinalIgnoreCase
                    ) )
                {
                    _ = Logger.LogAsync( $"Invalid extension for file {filePath}" );
                    return;
                }

                if ( _components?.Count > 0 )
                {
                    bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        "You already have a config loaded."
                        + " Do you want to load this instruction file anyway?"
                    );
                    if ( confirm != true )
                    {
                        return;
                    }
                }

                // Load components dynamically
                _components = Component.ReadComponentsFromFile( filePath );

                // Validate the components.
                await ProcessComponents( _components );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        public async void LoadMarkdown_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                string filePath = await OpenFile();
                using ( var reader = new StreamReader( filePath ) )
                {
                    string fileContents = await reader.ReadToEndAsync();
                    if ( _components?.Count > 0
                        && await ConfirmationDialog.ShowConfirmationDialog(
                            this,
                            "You already have a config loaded. Do you want to load the markdown anyway?"
                        )
                        != true )
                    {
                        return;
                    }

                    _components = ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) );
                    await ProcessComponents( _components );
                }
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void BrowseSourceFiles_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                var button = (Button)sender;
                // Get the item's data context based on the clicked button
                Instruction thisInstruction = (Instruction)button.DataContext
                    ?? throw new NullReferenceException( "Could not find instruction instance" );

                // Get the TextBox associated with the current item
                var textBox = (TextBox)button.Tag;

                // Open the file dialog to select a file
                List<string> files = await OpenFiles();
                if ( files == null )
                {
                    _ = Logger.LogVerboseAsync(
                        "No files chosen in BrowseSourceFiles_Click, returning to previous values"
                    );
                    return;
                }

                if ( files.Any( string.IsNullOrEmpty ) )
                {
                    await Logger.LogExceptionAsync(
                        new ArgumentOutOfRangeException(
                            nameof( files ),
                            $"Invalid files found. Please report this issue to the developer: [{string.Join( ",", files )}]"
                        )
                    );
                }

                // Replace path with prefixed variables.
                for ( int i = 0; i < files.Count; i++ )
                {
                    string filePath = files[i];
                    files[i] = MainConfig.SourcePath != null ? Utility.RestoreCustomVariables( filePath ) : filePath;
                }

                if ( MainConfig.SourcePath == null )
                {
                    _ = Logger.LogWarningAsync(
                        "Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set prior."
                    );
                }

                thisInstruction.Source = files;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void BrowseDestination_Click( [CanBeNull] object sender, RoutedEventArgs e )
        {
            try
            {
                Button button = (Button)sender ?? throw new InvalidOperationException();
                Instruction thisInstruction = (Instruction)button.DataContext
                    ?? throw new NullReferenceException( "Could not find instruction instance" );

                // Open the file dialog to select a file
                string filePath = await OpenFolder();
                if ( filePath == null )
                {
                    _ = Logger.LogVerboseAsync(
                        "No file chosen in BrowseDestination_Click."
                        + $" Will continue using '{thisInstruction.Destination}'"
                    );
                    return;
                }

                if ( MainConfig.SourcePath == null )
                {
                    _ = Logger.LogAsync(
                        "Directories not set, setting raw folder path without custom variable <<kotorDirectory>>"
                    );
                    thisInstruction.Destination = filePath;
                    return;
                }

                thisInstruction.Destination = Utility.RestoreCustomVariables( filePath );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void GenerateGuidButton_Click( object sender, RoutedEventArgs e )
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

        private async void SaveButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent == null )
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

                (bool success, string output) = SaveChanges();
                if ( !success )
                {
                    await InformationDialog.ShowInformationDialog( this, output );
                }

                RefreshTreeView();
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private async void ValidateButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( MainConfigInstance == null || MainConfig.DestinationPath == null )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "Please set your directories first"
                    );
                    return;
                }

                await Logger.LogAsync( "Running validation of all components, this might take a while..." );

                bool success = true;
                foreach ( Component component in _components )
                {
                    var validator = new ComponentValidation( component );
                    await Logger.LogVerboseAsync( $" == Validating {component.Name} == " );
                    success &= validator.Run();
                }

                // Ensure necessary directories are writable.
                bool isWritable = Utility.IsDirectoryWritable( MainConfig.DestinationPath )
                    && Utility.IsDirectoryWritable( MainConfig.SourcePath );

                string informationMessage = "There were issues with your instructions file."
                    + " Please review the output window for more information.";

                if ( !isWritable )
                {
                    informationMessage = "The Mod directory and/or the KOTOR directory are not writable."
                        + " Please ensure administrative privileges or reinstall KOTOR"
                        + " to a directory with write access.";
                }

                if ( success && isWritable )
                {
                    informationMessage = "No issues found."
                        + " If you encounter any problems during the installation, please contact the developer.";
                }

                await InformationDialog.ShowInformationDialog( this, informationMessage );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void AddComponentButton_Click( object sender, RoutedEventArgs e )
        {
            // Create a new default component with a new GUID
            try
            {
                Component newComponent
                    = Component.DeserializeTomlComponent(
                        Component.DefaultComponent + Instruction.DefaultInstructions
                    )
                    ?? throw new NullReferenceException( "Could not deserialize default template" );

                newComponent.Guid = Guid.NewGuid();
                newComponent.Name = "new mod_" + Path.GetRandomFileName();
                // Add the new component to the collection
                _components.Add( newComponent );
                _currentComponent = newComponent;

                // Load into the editor
                LoadComponentDetails( newComponent );

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private void RefreshComponents_Click( object sender, RoutedEventArgs e ) => RefreshTreeView();

        private void RemoveComponentButton_Click( object sender, RoutedEventArgs e )
        {
            // Get the selected component from the TreeView
            try
            {
                if ( _currentComponent == null )
                {
                    Logger.LogVerbose( "No component loaded into editor - nothing to remove." );
                    return;
                }

                // Remove the selected component from the collection
                _ = _components.Remove( _currentComponent );
                _currentComponent = null;

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private async void SetDirectories_Click( object sender, RoutedEventArgs e )
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
                    "Please select your mod directory (where the archives live)."
                );
                chosenFolder = await OpenFolder();
                if ( chosenFolder != null )
                {
                    var modDirectory = new DirectoryInfo( chosenFolder );
                    MainConfigInstance.sourcePath = modDirectory;
                }
            }
            catch ( ArgumentNullException )
            {
                _ = Logger.LogVerboseAsync( "User cancelled selecting folder" );
            }
        }

        private async void InstallModSingle_Click( object sender, RoutedEventArgs e )
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

                if ( MainConfigInstance == null || MainConfig.DestinationPath == null )
                {
                    var informationDialog = new InformationDialog { InfoText = "Please set your directories first" };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                if ( _currentComponent == null )
                {
                    var informationDialog = new InformationDialog
                    {
                        InfoText = "Please choose a mod to install from the left list first"
                    };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                if ( _currentComponent.Directions != null )
                {
                    bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        _currentComponent.Directions + Environment.NewLine
                        + Environment.NewLine
                        + "Press Yes to execute these directions now."
                    );
                    if ( confirm != true )
                    {
                        await Logger.LogAsync( $"User cancelled install of '{_currentComponent.Name}'" );
                        return;
                    }
                }

                try
                {
                    _installRunning = true;
                    Component.InstallExitCode exitCode = await Task.Run(
                        () => _currentComponent.InstallAsync(
                            _components
                        )
                    );
                    if ( exitCode == 0 )
                    {
                        await InformationDialog.ShowInformationDialog(
                            this,
                            $"There was a problem installing '{_currentComponent.Name}':"
                            + Environment.NewLine
                            + Utility.GetEnumDescription( exitCode )
                            + Environment.NewLine
                            + Environment.NewLine
                            + " Check the output window for details."
                        );
                    }
                    else
                    {
                        await Logger.LogAsync( $"Successfully installed '{_currentComponent.Name}'" );
                    }

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

        private bool _progressWindowClosed = false;

        private async void StartInstall_Click( object sender, RoutedEventArgs e )
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

                if ( MainConfigInstance == null || MainConfig.DestinationPath == null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Please set your directories first" );
                    return;
                }

                if ( _components.Count == 0 )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "No instructions loaded! Press 'Load Instructions File' or create some instructions first."
                    );
                    return;
                }

                try
                {
                    _ = Logger.LogAsync( "Start installing all mods..." );
                    _installRunning = true;

                    await Logger.LogAsync( "Running validation of all components, this might take a while..." );

                    bool valSuccess = true;
                    foreach ( Component component in _components )
                    {
                        var validator = new ComponentValidation( component );
                        await Logger.LogVerboseAsync( $" == Validating '{component.Name}' == " );
                        valSuccess &= validator.Run();
                    }

                    // Ensure necessary directories are writable.
                    bool isWritable = Utility.IsDirectoryWritable( MainConfig.DestinationPath )
                        && Utility.IsDirectoryWritable( MainConfig.SourcePath );

                    string informationMessage = "There were issues with your instructions file."
                        + " Please review the output window for more information."
                        + " Absolutely no files were modified during this process.";

                    if ( !isWritable )
                    {
                        informationMessage = "The Mod directory and/or the KOTOR directory are not writable."
                            + " Please ensure administrative privileges or reinstall KOTOR"
                            + " to a directory of which you have write access.";
                    }

                    if ( !valSuccess || !isWritable )
                    {
                        await InformationDialog.ShowInformationDialog( this, informationMessage );
                        return;
                    }

                    if ( await ConfirmationDialog.ShowConfirmationDialog( this, "Really install all mods?" ) != true )
                    {
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

                    var progressWindow = new ProgressWindow();
                    progressWindow.Closed += ProgressWindowClosed;
                    progressWindow.ProgressBar.Value = 0;
                    progressWindow.Show();

                    for ( int index = 0; index < _components.Count; index++ )
                    {
                        if ( _progressWindowClosed )
                        {
                            _installRunning = false;
                            _ = Logger.LogAsync( "User cancelled install by closing the progress window." );
                            return;
                        }

                        Component component = _components[index];
                        await Dispatcher.UIThread.InvokeAsync(
                            async () =>
                            {
                                progressWindow.ProgressTextBlock.Text = $"Installing '{component.Name}'..." + Environment.NewLine
                                    + Environment.NewLine
                                    + "Executing the provided directions..." + Environment.NewLine
                                    + Environment.NewLine
                                    + component.Directions;

                                double percentComplete = (double)index / _components.Count;
                                progressWindow.ProgressBar.Value = percentComplete;
                                progressWindow.InstalledRemaining.Text = $"{index}/{_components.Count + 1} Components Installed";
                                progressWindow.PercentCompleted.Text = $"{(int)percentComplete}%";


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

                        await Logger.LogAsync( $"Start Install of '{component.Name}'..." );
                        Component.InstallExitCode exitCode = await component.InstallAsync( _components );
                        await Logger.LogVerboseAsync( $"Install of '{component.Name}' finished with exit code {exitCode}" );

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

        private void ProgressWindowClosed( object sender, EventArgs e )
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

        private async void DocsButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "Please select a component from the list or create a new one first."
                    );
                    return;
                }

                string file = await SaveFile( new List<string>( 65535 ) { "txt" } );
                if ( file == null )
                {
                    return;
                }

                string docs = Component.GenerateModDocumentation( _components );
                await SaveDocsToFileAsync( file, docs );
                string message = $"Saved documentation of {_components.Count} mods to '{file}'";
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

        private static async Task SaveDocsToFileAsync( string filePath, string documentation )
        {
            try
            {
                await new StreamWriter( filePath ).WriteAsync( documentation );
            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
            }
        }

        private void TabControl_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if ( !( ( sender as TabControl )?.SelectedItem is TabItem selectedItem ) )
            {
                return;
            }

            if ( selectedItem?.Header == null )
            {
                return;
            }

            // Don't show content of any tabs (except the hidden one) if there's no content.
            if ( _components.Count == 0 || LeftTreeView.SelectedItem == null )
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

        private async void LoadComponentDetails
        (
            [CanBeNull] Component selectedComponent,
            bool confirmation = true
        )
        {
            try
            {
                if ( selectedComponent == null || RawEditTextBox == null )
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
                if ( InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue )
                {
                    TabControl.SelectedItem = GuiEditTabItem;
                }

                // populate raw editor
                RawEditTextBox.Text = _originalContent;
                // this tracks the currently selected component.
                _currentComponent = selectedComponent;
                // interestingly the variable 'ComponentsItemsControl' is already defined in this scope, but accessing it directly doesn't function the same.
                ItemsControl componentsItemsControl = this.FindControl<ItemsControl>( "ComponentsItemsControl" );
                // bind the selected component to the gui editor
                componentsItemsControl.Items = new ObservableCollection<Component> { selectedComponent };
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void RawEditTextBox_LostFocus( object sender, RoutedEventArgs e ) => e.Handled = true;

        private bool CheckForChanges()
        {
            string currentContent = RawEditTextBox.Text;
            return !string.Equals( currentContent, _originalContent );
        }

        private (bool, string Message) SaveChanges()
        {
            try
            {
                // Get the selected component from the tree view
                if ( _currentComponent == null )
                {
                    return (false, "TreeViewItem does not correspond to a valid Component"
                        + Environment.NewLine
                        + "Please report this issue to a developer, this should never happen.");
                }

                Component newComponent = Component.DeserializeTomlComponent( RawEditTextBox.Text );

                // Find the corresponding component in the collection
                int index = _components.IndexOf( _currentComponent );
                // if not selected, find the index of the _currentComponent.
                if ( index < 0 || index >= _components.Count )
                {
                    index = _components.FindIndex( c => c.Equals( _currentComponent ) );
                }

                if ( index < 0 && _currentComponent == null )
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
                _components[index] = newComponent
                    ?? throw new InvalidDataException(
                        "Could not deserialize raw text into a Component instance in memory."
                    );

                RefreshTreeView(); // Refresh the tree view to reflect the changes
                return (true,
                    $"Saved {newComponent.Name} successfully. Refer to the output window for more information.");
            }
            catch ( InvalidDataException ex )
            {
                return (
                    false,
                    ex.Message + Environment.NewLine + "Refer to the output window for details."
                );
            }
            catch ( Exception ex )
            {
                const string customMessage = "An unexpected exception was thrown. Please report this to the developer.";
                Logger.LogException( ex, customMessage );
                return (
                    false,
                    customMessage + Environment.NewLine + "Refer to the output window for details."
                );
            }
        }

        private void MoveTreeViewItem
            ( ItemsControl parentItemsControl, TreeViewItem selectedTreeViewItem, int newIndex )
        {
            try
            {
                List<Component> componentsList = _components; // Use the original components list
                int currentIndex = componentsList.IndexOf( (Component)selectedTreeViewItem.Tag );

                if ( currentIndex == -1 || newIndex < 0 || newIndex >= componentsList.Count )
                {
                    return;
                }

                componentsList.RemoveAt( currentIndex );
                componentsList.Insert( newIndex, (Component)selectedTreeViewItem.Tag );
                LeftTreeView.SelectedItem = selectedTreeViewItem;

                // Update the visual tree directly to reflect the changes
                var parentItemsCollection = (AvaloniaList<object>)parentItemsControl.Items;
                parentItemsCollection.Move( currentIndex, newIndex );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private void MoveUpButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
                    || !( selectedTreeViewItem.Parent is ItemsControl parentItemsControl ) )
                {
                    return;
                }

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList()
                    .IndexOf( selectedTreeViewItem );
                MoveTreeViewItem( parentItemsControl, selectedTreeViewItem, currentIndex - 1 );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private void MoveDownButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
                    || !( selectedTreeViewItem.Parent is ItemsControl parentItemsControl ) )
                {
                    return;
                }

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList()
                    .IndexOf( selectedTreeViewItem );
                MoveTreeViewItem( parentItemsControl, selectedTreeViewItem, currentIndex + 1 );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private async void SaveModFile_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                string filePath = await SaveFile();
                if ( filePath == null )
                {
                    return;
                }

                TreeViewItem rootItem = Enumerable.OfType<TreeViewItem>( LeftTreeView.Items ).FirstOrDefault();
                if ( rootItem != null )
                {
                    WriteTreeViewItemsToFile( new List<TreeViewItem> { rootItem }, filePath );
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        private void RefreshTreeView()
        {
            try
            {
                // Create the root item for the tree view
                var rootItem = new TreeViewItem { Header = "Components" };

                // Iterate over the components and create tree view items
                foreach ( Component component in _components )
                {
                    CreateTreeViewItem( component, rootItem );
                }

                // Set the root item as the single item of the tree view
                LeftTreeView.Items = new AvaloniaList<object> { rootItem };

                // Expand the root item to automatically expand the tree view
                rootItem.IsExpanded = true;

                if ( _components.Count == 0 )
                {
                    TabControl.SelectedItem = InitialTab;
                }
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }

        private void CreateTreeViewItem( Component component, ItemsControl parentItem )
        {
            try
            {
                // Check if the component item is already added to the parent item
                string componentName = component.Name;
                TreeViewItem existingItem = null;
                foreach ( object item in parentItem.Items )
                {
                    if ( !( item is TreeViewItem treeViewItem ) )
                    {
                        continue;
                    }

                    string headerString = treeViewItem.Header?.ToString();
                    if ( headerString == null )
                    {
                        continue;
                    }

                    if ( !headerString.Equals( componentName, StringComparison.Ordinal ) )
                    {
                        continue;
                    }

                    existingItem = treeViewItem;
                    break;
                }


                if ( existingItem != null )
                {
                    // Update the existing item's tag with the component
                    existingItem.Tag = component;
                    return;
                }

                // Create a new tree view item for the component
                var componentItem = new TreeViewItem
                {
                    Header = component.Name,
                    Tag = component // this allows us to access the item later
                };

                // Assign the ItemClickCommand to the componentItem.
                // This loads the component into the editor when clicked.
                componentItem.Tapped += ( sender, e ) =>
                {
                    if ( _selectedComponents.Contains( component ) )
                    {
                        _ = _selectedComponents.Remove( component );
                    }

                    ItemClickCommand.Execute( component );
                };

                // Add the component item to the parent item
                ( (AvaloniaList<object>)parentItem.Items ).Add( componentItem );

                // Check if the component has dependencies
                if ( component?.Dependencies == null || component.Dependencies.Count == 0 )
                {
                    return;
                }

                // Iterate over the dependencies and create tree view items
                foreach ( string dependencyGuid in component.Dependencies )
                {
                    try
                    {
                        // Find the dependency in the components list
                        Component dependency = _components.Find( c => c.Guid == new Guid( dependencyGuid ) );
                        if ( dependency == null )
                        {
                            continue;
                        }

                        // Create the dependency tree view item
                        CreateTreeViewItem( dependency, componentItem );
                    }
                    catch ( FormatException ex )
                    {
                        // Usually catches invalid guid from the user
                        Logger.LogException( ex );
                    }
                }
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, "Unexpected exception while creating tree view item" );
            }
        }

        private static void WriteTreeViewItemsToFile( List<TreeViewItem> items, string filePath )
        {
            string randomFileName = Path.GetFileNameWithoutExtension( Path.GetRandomFileName() );
            filePath = filePath ?? $"modconfig_{randomFileName}.toml";
            Logger.Log( $"Creating backup mod config at {filePath}" );

            using ( var writer = new StreamWriter( filePath ) )
            {
                foreach ( TreeViewItem item in items )
                {
                    WriteTreeViewItemToFile( item, writer, maxDepth: 1 );
                }
            }
        }

        private static void WriteTreeViewItemToFile
            ( ItemsControl item, TextWriter writer, int depth = 0, int maxDepth = int.MaxValue )
        {
            if ( item.Tag is Component component )
            {
                string tomlContents = component.SerializeComponent();
                writer.WriteLine( tomlContents );
            }

            if ( depth >= maxDepth || item.Items == null )
            {
                return;
            }

            foreach ( TreeViewItem childItem in item.Items.OfType<TreeViewItem>() )
            {
                WriteTreeViewItemToFile( childItem, writer, depth + 1, maxDepth );
            }
        }

        // used for leftTreeView double-click event.
        private void ItemClick( object parameter )
        {
            if ( !( parameter is Component component ) )
            {
                return;
            }

            if ( !_selectedComponents.Contains( component ) )
            {
                _selectedComponents.Add( component );
            }

            LoadComponentDetails( component );
        }

        private async void AddNewInstruction_Click( object sender, RoutedEventArgs e )
        {
            var addButton = (Button)sender;
            var thisInstruction = addButton.Tag as Instruction;
            var thisComponent = addButton.Tag as Component;
            if ( thisInstruction == null && thisComponent == null )
            {
                await Logger.LogAsync( "Cannot find instruction instance from button." );
                return;
            }

            if ( _currentComponent == null )
            {
                await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                return;
            }

            int index;
            if ( thisInstruction == null )
            {
                thisInstruction = new Instruction();
                index = _currentComponent.Instructions.Count;
            }
            else
            {
                index = _currentComponent.Instructions.IndexOf( thisInstruction );
            }

            _currentComponent.CreateInstruction( index );
            await Logger.LogVerboseAsync( $"Instruction '{thisInstruction.Action}' created at index #{index}" );

            LoadComponentDetails( _currentComponent );
        }

        private async void DeleteInstruction_Click( object sender, RoutedEventArgs e )
        {
            var addButton = (Button)sender;
            if ( !( addButton.Tag is Instruction thisInstruction ) )
            {
                await Logger.LogAsync( "Cannot find instruction instance from button." );
                return;
            }

            if ( _currentComponent == null )
            {
                await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                return;
            }

            int index = _currentComponent.Instructions.IndexOf( thisInstruction );

            _currentComponent.DeleteInstruction( index );
            await Logger.LogVerboseAsync( $"instruction '{thisInstruction.Action}' deleted at index #{index}" );

            LoadComponentDetails( _currentComponent );
        }

        private async void MoveInstructionUp_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                var addButton = (Button)sender;
                if ( !( addButton.Tag is Instruction thisInstruction ) )
                {
                    await Logger.LogAsync( "Cannot find instruction instance from button." );
                    return;
                }

                if ( _currentComponent == null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

                int index = _currentComponent.Instructions.IndexOf( thisInstruction );

                _currentComponent.MoveInstructionToIndex( thisInstruction, index - 1 );
                LoadComponentDetails( _currentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        private async void MoveInstructionDown_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                var addButton = (Button)sender;
                if ( !( addButton.Tag is Instruction thisInstruction ) )
                {
                    await Logger.LogAsync( "Cannot find instruction instance from button." );
                    return;
                }

                if ( _currentComponent == null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Load a component first" );
                    return;
                }

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

            public RelayCommand( Action<object> execute, [CanBeNull] Func<object, bool> canExecute = null )
            {
                _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
                _canExecute = canExecute;
            }

#pragma warning disable CS0067 // warning is incorrect - it's used internally.
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute( object parameter ) => _canExecute == null || _canExecute( parameter );
            public void Execute( object parameter ) => _execute( parameter );
        }

        private void OpenOutputWindow_Click( object sender, RoutedEventArgs e )
        {
            if ( _outputWindow != null && _outputWindow.IsVisible )
            {
                _outputWindow.Close();
            }

            _outputWindow = null;

            if ( _outputWindow == null )
            {
                _outputWindow = new GUI.OutputWindow();
                _outputWindow.Show();
            }
        }

        private void StyleComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            ComboBox comboBox = (ComboBox)sender;
            ComboBoxItem selectedItem = (ComboBoxItem)comboBox.SelectedItem;

            string stylePath = (string)selectedItem?.Tag;

            if ( stylePath == null )
            {
                return;
            }

            this.Styles[0] = new Style();

            if ( stylePath.Equals( "default" ) )
            {
                return;
            }

            Uri styleUriPath = new Uri( "avares://KOTORModSync.GUI" + stylePath );

            // Apply the selected style dynamically
            this.Styles[0] = new StyleInclude( styleUriPath )
            {
                Source = styleUriPath
            };
        }

    }
}
