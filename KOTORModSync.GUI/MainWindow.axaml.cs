// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
    public partial class MainWindow : Window
    {
        private List<Component> _components = new List<Component>();
        private readonly ObservableCollection<Component> _selectedComponents = new ObservableCollection<Component>();
        private ObservableCollection<string> _selectedComponentProperties;
        private string _originalContent;
        private Component _currentComponent;
        private bool _installRunning;
        public MainConfig MainConfigInstance { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            MainConfigInstance = new MainConfig();
            MainConfigStackPanel = this.FindControl<StackPanel>( "MainConfigStackPanel" );
            MainConfigStackPanel.DataContext = MainConfigInstance;
            DataContext = this;
            //Testwindow();
        }

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
            LeftTreeView = this.FindControl<TreeView>( "LeftTreeView" );

            RightTextBox = this.FindControl<TextBox>( "RightTextBox" );
            RightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            RightTextBox.DataContext = _selectedComponentProperties;

            TabControl = this.FindControl<TabControl>( "TabControl" );
            RawEditTabItem = this.FindControl<TabItem>( "RawEditTabItem" );
            GuiEditTabItem = this.FindControl<TabItem>( "GuiEditTabItem" );
            InitialTab = this.FindControl<TabItem>( "InitialTab" );
            ApplyEditorButton = this.FindControl<Button>( "ApplyEditorButton" );
            _selectedComponentProperties = new ObservableCollection<string>();
        }

        public static IControl Build( object data )
        {
            try
            {
                // Create a dictionary to keep track of child TreeViewItems
                var childItems = new Dictionary<string, TreeViewItem>( 10000 );
                if ( !( data is Component component ) )
                    throw new InvalidCastException( "data variable should always be a Component." );

                // If no dependencies we can return here.
                if ( component.Dependencies == null || component.Dependencies.Count == 0 )
                    return new TextBlock { Text = component.Name }; // Use a TextBlock for components without dependencies

                // Create a TreeViewItem for the component
                var treeViewItem = new TreeViewItem { Header = component.Name };

                // Check if the component has any dependencies
                foreach ( string dependency in component.Dependencies )
                {
                    if ( childItems.ContainsKey( dependency ) )
                        continue;

                    // Create a new child TreeViewItem for each unique dependency
                    var childItem = new TreeViewItem { Header = dependency };
                    childItems.Add( dependency, childItem );
                }

                // Add child TreeViewItems to the parent TreeViewItem
                var items = treeViewItem.Items as IList;
                foreach ( TreeViewItem childItem in childItems.Values
                             .Where( childItem => childItem == null
                                 ? throw new ArgumentNullException( nameof( childItem ) )
                                 : items != null
                             ) )
                {
                    if ( items == null )
                        continue;

                    _ = items.Add( childItem );
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
                    return result[0]; // Retrieve the first selected file path
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task<List<string>> OpenFiles()
        {
            try
            {
                var filters = new List<FileDialogFilter>( 10 ) { new FileDialogFilter { Name = "All Files", Extensions = { "*" } } };

                string[] filePaths = await ShowFileDialog( false, filters, true );
                if ( filePaths == null )
                {
                    await Logger.LogVerboseAsync( "User did not select any files." );
                }
                else
                {
                    await Logger.LogAsync( $"Selected files: {string.Join( ", ", filePaths )}" );
                    return filePaths.ToList();
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task<string> OpenFolder()
        {
            try
            {
                string[] thisFolder = await ShowFileDialog( true, null );
                return thisFolder[0];
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        [ItemCanBeNull]
        private async Task<string> SaveFile( List<string> defaultExt = null )
        {
            try
            {
                if ( defaultExt == null )
                {
                    defaultExt = new List<string>() { "toml", "tml" };
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
                    await Logger.LogAsync( "Could not open dialog - parent window not found" );
                }
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task<string[]> ShowFileDialog(
            bool isFolderDialog,
            List<FileDialogFilter> filters,
            bool allowMultiple = false
        )
        {
            try
            {
                if ( !( VisualRoot is Window parent ) )
                {
                    await Logger.LogAsync( $"Could not open {( isFolderDialog ? "folder" : "file" )} dialog - parent window not found" );
                    return default;
                }

                string[] results = isFolderDialog
                    ? ( new[] { await new OpenFolderDialog().ShowAsync( parent ) } )
                    : await new OpenFileDialog() { AllowMultiple = allowMultiple, Filters = filters }.ShowAsync( parent );

                if ( results == null || results.Length == 0 )
                {
                    await Logger.LogVerboseAsync( "User did not make a selection" );
                    return default;
                }

                await Logger.LogAsync( $"Selected {( isFolderDialog ? "folder" : "file" )}: {string.Join( ", ", results )}" );
                return results;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task ProcessComponents( List<Component> components )
        {
            if ( !( components?.Count > 0 ) )
                return;

            // Clear existing items in the tree view
            LeftTreeView.Items = new AvaloniaList<object>();

            // Create the root item for the tree view
            var rootItem = new TreeViewItem { Header = "Components" };

            int i = 0;
            foreach ( Component component in components )
            {
                CreateTreeViewItem( component, rootItem );

                // Check for duplicate GUID
                Guid componentGuid = component.Guid;
                Component duplicateComponent = components.Find( c => c.Guid == componentGuid && c != component );

                if ( duplicateComponent == null )
                    continue;

                if ( !Guid.TryParse( duplicateComponent.Guid.ToString(), out Guid outGuid ) )
                {
                    if ( MainConfig.AttemptFixes )
                        duplicateComponent.Guid = Guid.NewGuid();
                    await Logger.LogWarningAsync( $"Invalid GUID for component '{component.Name}' got '{component.Guid}'" );
                }

                string message = $"Component '{component.Name}' has duplicate GUID with component '{duplicateComponent.Name}'";
                await Logger.LogAsync( message );

                bool? confirm = i >= 2
                    || ( await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        message + $".\r\n"
                        + $"Assign random GUID to '{duplicateComponent.Name}'? (default: NO)"
                    ) ) == true;

                if ( confirm == true )
                {
                    i++;
                    duplicateComponent.Guid = Guid.NewGuid();
                    await Logger.LogVerboseAsync( $"Replaced GUID of component {duplicateComponent.Name}" );
                }
                else
                {
                    await Logger.LogVerboseAsync( $"User canceled GUID replacement for component {duplicateComponent.Name}" );
                }
            }

            await Dispatcher.UIThread.InvokeAsync( () =>
            {
                // Create a collection to hold the root item
                var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

                // Set the root item collection as the items source of the tree view
                LeftTreeView.Items = rootItemsCollection;

                // Expand the tree. Too lazy to figure out the proper way.
                IEnumerator treeEnumerator = LeftTreeView.Items.GetEnumerator();
                _ = treeEnumerator.MoveNext();
                LeftTreeView.ExpandSubTree( (TreeViewItem)treeEnumerator.Current );
            } );
        }

        private async void LoadInstallFile_Click( object sender, RoutedEventArgs e )
        {
            // Open the file dialog to select a file
            try
            {
                string filePath = await OpenFile();
                if ( string.IsNullOrEmpty( filePath ) )
                    return;

                // Verify the file type
                string fileExtension = Path.GetExtension( filePath );
                if ( !new List<string> { ".toml", ".tml", ".txt" }.Contains( fileExtension, StringComparer.OrdinalIgnoreCase ) )
                {
                    await Logger.LogAsync( $"Invalid extension for file {filePath}" );
                    return;
                }

                // Load components dynamically
                _components = FileHelper.ReadComponentsFromFile( filePath );

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
                    if ( _components?.Count > 0 )
                    {
                        if ( await ConfirmationDialog.ShowConfirmationDialog(
                                this,
                                "You already have a config loaded. Do you want to load the markdown anyway?"
                            ) == true )
                        {
                            return;
                        }
                    }

                    this._components = ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) );
                    await ProcessComponents( _components );
                }
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
                throw;
            }
        }

        private async void BrowseSourceFiles_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                var button = (Button)sender;
                // Get the item's data context based on the clicked button
                var thisInstruction = (Instruction)button.DataContext;

                if ( thisInstruction == null )
                {
                    await Logger.LogAsync( "Could not find instruction instance during BrowseSourceFiles_Click" );
                    return;
                }

                // Get the TextBox associated with the current item
                var textBox = (TextBox)button.Tag;

                // Open the file dialog to select a file
                List<string> files = await OpenFiles();
                if ( files == null )
                {
                    await Logger.LogAsync( "No files chosen in BrowseSourceFiles_Click, returning to previous values" );
                    return;
                }

                if ( files.Any( string.IsNullOrEmpty ) )
                {
                    await Logger.LogExceptionAsync(
                        new ArgumentOutOfRangeException(
                            nameof( files ),
                            $"Invalid files found, please report this to the developer: '{files}'"
                        ) );
                }

                // Replace path with prefixed variables.
                for ( int i = 0; i < files.Count; i++ )
                {
                    string filePath = files[i];
                    files[i] = MainConfig.SourcePath != null ? Utility.RestoreCustomVariables( filePath ) : filePath;
                }

                if ( MainConfig.SourcePath == null )
                    await Logger.LogAsync( "Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set prior." );
                thisInstruction.Source = files;
            }
            catch ( ArgumentNullException ex )
            { await Logger.LogVerboseAsync( ex.Message ); }
            catch ( Exception ex )
            { await Logger.LogExceptionAsync( ex ); }
        }

        private async void BrowseDestination_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                Button button = (Button)sender ?? throw new InvalidOperationException();
                Instruction thisInstruction = (Instruction)button.DataContext ?? throw new InvalidDataException( "Could not find instruction instance during BrowseSourceFiles_Click" );

                // Get the TextBox associated with the current item
                //var textBox = (TextBox)button.Tag;

                // Open the file dialog to select a file
                string filePath = await OpenFolder() ?? throw new ArgumentNullException( $"No file chosen in BrowseDestination_Click. Will continue using {thisInstruction.Destination}" );

                if ( MainConfig.SourcePath == null )
                {
                    await Logger.LogAsync(
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
                if ( _currentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, "You must select a component from the list, or create one, before saving." );
                    return;
                }

                await Logger.LogVerboseAsync( $"Selected {_currentComponent.Name}" );

                if ( !CheckForChanges() )
                    return;

                bool? confirmationResult = await ConfirmationDialog.ShowConfirmationDialog( this, "Are you sure you want to save?" );
                if ( confirmationResult == true )
                    return;

                string message = SaveChanges() ? "Saved successfully. Check the output window for more information." : "There were some problems with your syntax, please check the output window.";
                await InformationDialog.ShowInformationDialog( this, message );
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
                        "Please set your directories first" );
                    return;
                }

                bool success = true;
                foreach ( Component component in _components )
                {
                    var validator = new ComponentValidation( component );
                    success &= validator.Run();
                }

                // Ensure necessary directories are writable.
                bool isWritable = Utility.IsDirectoryWritable( MainConfig.DestinationPath )
                    && Utility.IsDirectoryWritable( MainConfig.SourcePath );

                string informationMessage
                    = "There were problems with your instructions file, please check the output window for details.";
                if ( !isWritable )
                {
                    informationMessage
                        = "Your Mod directory and/or your KOTOR directory are not writable! Try running as admin?";
                }

                if ( success && isWritable )
                {
                    informationMessage
                        = "No issues found. If you run into any issues during an install please notify the developer";
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
                Component newComponent = FileHelper.DeserializeTomlComponent( Component.DefaultComponent + Instruction.DefaultInstructions );
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
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem ) ||
                    !( selectedTreeViewItem.Tag is Component selectedComponent ) )
                {
                    return;
                }

                // Remove the selected component from the collection
                _ = _components.Remove( selectedComponent );
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
                await InformationDialog.ShowInformationDialog( this, "Please select your KOTOR(2) directory. (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")" );
                string chosenFolder = await OpenFolder();
                var kotorInstallDir = new DirectoryInfo( chosenFolder );
                MainConfigInstance.destinationPath = kotorInstallDir;
                await InformationDialog.ShowInformationDialog( this, "Please select your mod directory (where the archives live)." );
                chosenFolder = await OpenFolder();
                var modDirectory = new DirectoryInfo( chosenFolder );
                MainConfigInstance.sourcePath = modDirectory;
            }
            catch ( ArgumentNullException )
            {
                await Logger.LogAsync( "User cancelled selecting folder" );
                return;
            }
        }

        private async void InstallModSingle_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( MainConfigInstance == null || MainConfig.DestinationPath == null )
                {
                    var informationDialog = new InformationDialog { InfoText = "Please set your directories first" };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                Component thisComponent;
                if ( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent )
                {
                    thisComponent = (Component)selectedTreeViewItem.Tag;
                }
                else
                {
                    var informationDialog = new InformationDialog { InfoText = "Please choose a mod to install from the left list first" };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                if ( thisComponent.Directions != null )
                {
                    bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        thisComponent.Directions + "\r\n\r\n Press Yes to execute these directions now."
                    );
                    if ( confirm == true )
                    {
                        await Logger.LogAsync( $"User cancelled install of {thisComponent.Name}" );
                        return;
                    }
                }

                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "There's already another installation running, please check the output window."
                    );
                    return;
                }

                _installRunning = true;
                var confirmationDialogCallback = new ConfirmationDialogCallback( this );
                var optionsDialogCallback = new OptionsDialogCallback( this );
                (bool success, Dictionary<FileInfo, SHA1> originalChecksums) = await Task.Run( () => thisComponent.ExecuteInstructions( confirmationDialogCallback, optionsDialogCallback, _components ) );
                if ( !success )
                    await InformationDialog.ShowInformationDialog( this, $"There was a problem installing {thisComponent.Name}, please check the output window" );
                else
                    await Logger.LogAsync( $"Successfully installed {thisComponent.Name}" );
                _installRunning = false;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                _installRunning = false;
            }
        }

        private async void StartInstall_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                if ( MainConfigInstance == null || MainConfig.DestinationPath == null )
                {
                    await InformationDialog.ShowInformationDialog( this, "Please set your directories first" );
                    return;
                }

                if ( _components.Count == 0 )
                {
                    await InformationDialog.ShowInformationDialog( this, "No instructions loaded! Press 'Load Instructions File' or create some instructions first." );
                    return;
                }

                if ( await ConfirmationDialog.ShowConfirmationDialog( this, "Really install all mods?" ) == true )
                {
                    return;
                }

                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        "There's already an installation running, please check the output window." );
                    return;
                }

                _installRunning = true;
                await Logger.LogAsync( "Start installing all mods..." );
                var progressWindow = new ProgressWindow();
                progressWindow.Closed += ProgressWindowClosed;
                progressWindow.progressBar.Value = 0;
                progressWindow.Show();

                foreach ( Component component in _components )
                {
                    await Dispatcher.UIThread.InvokeAsync( async () =>
                    {
                        progressWindow.progressTextBlock.Text = $"Installing {component.Name}...\n\n"
                            + $"Executing the following directions:\n\n"
                            + $"{component.Directions}";
                        progressWindow.progressBar.Value = 0;

                        // Additional fallback options
                        await Task.Delay( 100 ); // Introduce a small delay
                        await Dispatcher.UIThread.InvokeAsync( () => { } ); // Invoke an empty action to ensure UI updates are processed
                        await Task.Delay( 50 ); // Introduce another small delay
                    } );

                    // Ensure the UI updates are processed
                    await Task.Yield();
                    await Task.Delay( 200 );

                    // Call the ExecuteInstructions method asynchronously using Task.Run
                    await Logger.LogAsync( $"Call ExecuteInstructions for {component.Name}..." );
                    (bool success, Dictionary<FileInfo, SHA1> originalChecksums)
                        = await component.ExecuteInstructions(
                            new ConfirmationDialogCallback( this ),
                            new OptionsDialogCallback( this ),
                            _components
                        );

                    if ( !success )
                    {
                        bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                            this,
                            $"There was a problem installing {component.Name},"
                            + $" please check the output window.\n\n"
                            + $"Continue with the next mod anyway?"
                        );
                        if ( confirm == true )
                            break;
                    }
                    else
                    {
                        await Logger.LogAsync( $"Successfully installed {component.Name}" );
                    }
                }

                progressWindow.Close();
                _installRunning = false;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                _installRunning = false;
            }
        }

        private void ProgressWindowClosed( object sender, EventArgs e )
        {
            if ( !( sender is ProgressWindow progressWindow ) )
                return;
            progressWindow.progressBar.Value = 0;
            progressWindow.Closed -= ProgressWindowClosed;
            progressWindow.Dispose();
        }

        private async void DocsButton_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                string file = await SaveFile( new List<string>( 65535 ) { "txt" } );
                if ( file == null )
                    return;

                string docs = Serializer.GenerateModDocumentation( _components );
                await SaveDocsToFile( file, docs );
                string message = $"Saved documentation of {_components.Count} mods to '{file}'";
                await InformationDialog.ShowInformationDialog( this, message );
                await Logger.LogAsync( message );
            }
            catch ( Exception ex )
            {
                await Logger.LogAsync( $"Error generating and saving documentation: {ex.Message}" );
                await InformationDialog.ShowInformationDialog( this, "An error occurred while generating and saving documentation." );
            }
        }

        private static async Task SaveDocsToFile( string filePath, string documentation )
        {
            try { await new StreamWriter( filePath ).WriteAsync( documentation ); }
            catch ( Exception e ) { await Logger.LogExceptionAsync( e ); }
        }

        private void TabControl_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if ( !( ( sender as TabControl )?.SelectedItem is TabItem selectedItem ) )
                return;
            if ( selectedItem?.Header == null )
                return;

            // Don't show content of any tabs (except the hidden one) if there's no content.
            if ( selectedItem != InitialTab && _components.Count == 0 )
            {
                TabControl.SelectedItem = InitialTab;
                return;
            }

            // Show/hide the appropriate content based on the selected tab
            if ( selectedItem.Header.ToString() == "Raw Edit" )
            {
                RightTextBox.IsVisible = true;
                ApplyEditorButton.IsVisible = true;
            }
            else if ( selectedItem.Header.ToString() == "GUI Edit" )
            {
                RightTextBox.IsVisible = false;
                ApplyEditorButton.IsVisible = false;
            }
        }

        private async void LoadComponentDetails(
            Component selectedComponent,
            bool confirmation = true
        )
        {
            try
            {
                if ( selectedComponent == null || RightTextBox == null )
                    return;

                // todo: figure out what we're doing with _originalComponent
                await Logger.LogVerboseAsync( $"Loading {selectedComponent.Name}..." );
                _originalContent = selectedComponent.SerializeComponent();
                if ( _originalContent.Equals( RightTextBox.Text )
                    && !string.IsNullOrEmpty( RightTextBox.Text )
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

                // ReSharper disable once BuiltInTypeReferenceStyleForMemberAccess
#pragma warning disable IDE0049 // Simplify Names

                // default to GuiEditTabItem.
                if ( InitialTab.IsSelected || TabControl.SelectedIndex == Int32.MaxValue )
                {
                    TabControl.SelectedItem = GuiEditTabItem;
                }

#pragma warning restore IDE0049 // Simplify Names

                // populate raw editor
                RightTextBox.Text = _originalContent;
                // this tracks the currently selected component.
                _currentComponent = selectedComponent;
                // interestingly the variable 'ComponentsItemsControl' is already defined in this scope, but accessing it directly doesn't function the same.
                ItemsControl componentsItemsControl = this.FindControl<ItemsControl>( "ComponentsItemsControl" );
                // bind the selected component to the gui editor
                componentsItemsControl.Items = new ObservableCollection<Component>
                {
                    selectedComponent
                };
            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void RightListBox_LostFocus( object sender, RoutedEventArgs e ) => e.Handled = true;

        private bool CheckForChanges()
        {
            string currentContent = RightTextBox.Text;
            return !string.Equals( currentContent, _originalContent );
        }

        private async void TextBox_LostFocus( object sender, RoutedEventArgs e )
        {
            var textBox = (TextBox)sender;

            // code might be needed in future changes to TextBox/AvaloniaUI
            // Retrieve the DataContext object
            /*var dataContext = textBox.DataContext;

            // Retrieve the bound property name
            var propertyInfo = dataContext.GetType().GetProperties()
                .FirstOrDefault(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsVirtual && p.PropertyType == typeof(string) && p.GetMethod.GetBaseDefinition().DeclaringType == p.GetMethod.DeclaringType);

            if (propertyInfo != null)
            {
                // Retrieve the updated value from the TextBox
                var updatedValue = textBox.Text;

                // Set the updated value to the bound property
                propertyInfo.SetValue(dataContext, updatedValue);
            }*/

            // Delay the collection update by a small amount
            // otherwise it's impossible to focus another textbox
            // another solution would be appreciated.
            await Task.Delay( 100 );
            if ( !textBox.IsFocused )
            {
                LoadComponentDetails( _currentComponent );
            }
        }

        private bool SaveChanges()
        {
            try
            {
                // Get the selected component from the tree view
                if ( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent )
                {
                    Component newComponent = FileHelper.DeserializeTomlComponent( RightTextBox.Text );

                    // Find the corresponding component in the collection
                    int index = _components.IndexOf( selectedComponent );
                    // if not selected, find the index of the _currentComponent.
                    if ( index < 0 || index >= _components.Count )
                    {
                        index = _components.FindIndex( c => c.Equals( _currentComponent ) );
                    }

                    if ( index < 0 && _currentComponent == null )
                    {
                        var ex = new IndexOutOfRangeException(
                            "Could not find index of component."
                            + " Ensure you single clicked on a component on the left before pressing save."
                            + " Please back up your work and try again."
                        );
                        Logger.LogException( ex );
                        return false;
                    }

                    // Update the properties of the component

                    _components[index] = newComponent
                        ?? throw new InvalidDataException(
                            "Could not deserialize raw text into a Component instance in memory."
                        );
                    RefreshTreeView(); // Refresh the tree view to reflect the changes
                    LeftTreeView.SelectedItem = newComponent; // Select the updated component in the tree view
                    return true;
                }
            }
            catch ( IndexOutOfRangeException ex )
            {
                Logger.LogException( ex );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }

            return false;
        }

        private void MoveTreeViewItem( ItemsControl parentItemsControl, TreeViewItem selectedTreeViewItem, int newIndex )
        {
            try
            {
                List<Component> componentsList = _components; // Use the original components list
                int currentIndex = componentsList.IndexOf( (Component)selectedTreeViewItem.Tag );

                if ( currentIndex == -1 || newIndex < 0 || newIndex >= componentsList.Count )
                    return;

                componentsList.RemoveAt( currentIndex );
                componentsList.Insert( newIndex, (Component)selectedTreeViewItem.Tag );
                LeftTreeView.SelectedItem = selectedTreeViewItem;

                // Update the visual tree directly to reflect the changes
                var parentItemsCollection = (AvaloniaList<object>)parentItemsControl.Items;
                parentItemsCollection.Move( currentIndex, newIndex );
            }
            catch ( ArgumentOutOfRangeException ex )
            {
                Logger.LogException( ex );
                Logger.Log( "Will fix above error in a future version - sorry." );
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

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf( selectedTreeViewItem );
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
                if ( !( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem ) ||
                    !( selectedTreeViewItem.Parent is ItemsControl parentItemsControl ) )
                {
                    return;
                }

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf( selectedTreeViewItem );
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

                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
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
                var rootItem = new TreeViewItem
                {
                    Header = "Components"
                };

                // Iterate over the components and create tree view items
                foreach ( Component component in _components )
                    CreateTreeViewItem( component, rootItem );

                // Set the root item as the single item of the tree view
                LeftTreeView.Items = new AvaloniaList<object> { rootItem };

                // Expand the root item to automatically expand the tree view
                rootItem.IsExpanded = true;

                //WriteTreeViewItemsToFile(new List<TreeViewItem> { rootItem }, null);
                //currentComponent = null;

                if ( _components.Count == 0 )
                    TabControl.SelectedItem = InitialTab;
            }
            catch ( ArgumentException ex )
            {
                Logger.LogException( ex );
                Logger.Log( "Ensure your config file does not have any duplicate mods defined." );
            }
        }

        private void CreateTreeViewItem( Component component, TreeViewItem parentItem )
        {
            try
            {
                // Check if the component item is already added to the parent item
                string componentName = component.Name;
                TreeViewItem existingItem = parentItem.Items.OfType<TreeViewItem>()
                    .FirstOrDefault(
                        item => item.Header.ToString()
                            .Equals( componentName, StringComparison.Ordinal )
                    );

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

                // Assign the ItemClickCommand to the componentItem
                componentItem.DoubleTapped += ( sender, e ) =>
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
                    return;

                // Iterate over the dependencies and create tree view items
                foreach ( string dependencyGuid in component.Dependencies )
                {
                    try
                    {
                        // Find the dependency in the components list
                        Component dependency = _components.Find( c => c.Guid == new Guid( dependencyGuid ) );

                        if ( dependency == null )
                            continue;
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
                Logger.LogException( new Exception( $"Error creating tree view item: {ex.Message}" ) );
            }
        }

        private static void WriteTreeViewItemsToFile( List<TreeViewItem> items, string filePath )
        {
            string randomFileName = Path.GetFileNameWithoutExtension( Path.GetRandomFileName() );
            filePath = filePath ?? $"modconfig_{randomFileName}.toml";
            Logger.Log( $"Creating backup modconfig at {filePath}" );

            using ( var writer = new StreamWriter( filePath ) )
            {
                foreach ( TreeViewItem item in items )
                    WriteTreeViewItemToFile( item, writer, maxDepth: 1 );
            }
        }

        private static void WriteTreeViewItemToFile( TreeViewItem item, TextWriter writer, int depth = 0, int maxDepth = int.MaxValue )
        {
            if ( item.Tag is Component component )
            {
                string tomlContents = component.SerializeComponent();
                writer.WriteLine( tomlContents );
            }

            if ( depth >= maxDepth || item.Items == null )
                return;

            foreach ( TreeViewItem childItem in item.Items.OfType<TreeViewItem>() )
                WriteTreeViewItemToFile( childItem, writer, depth + 1, maxDepth );
        }

        private RelayCommand _itemClickCommand;
        public ICommand ItemClickCommand => _itemClickCommand ?? ( _itemClickCommand = new RelayCommand( ItemClick ) );

        private void ItemClick( object parameter )
        {
            // used for leftTreeView doubleclick event.
            if ( !( parameter is Component component ) )
                return;

            if ( !_selectedComponents.Contains( component ) )
            {
                _selectedComponents.Add( component );
            }

            LoadComponentDetails( component );
        }

        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Func<object, bool> _canExecute;

            public RelayCommand( Action<object> execute, [CanBeNull] Func<object, bool> canExecute = null )
            {
                this._execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
                this._canExecute = canExecute;
            }

#pragma warning disable CS0067 // warning is incorrect - it's used internally.
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute( object parameter ) => _canExecute == null || _canExecute( parameter );
            public void Execute( object parameter ) => _execute( parameter );
        }
    }

    public class ComboBoxItemConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) => value is string action ? new ComboBoxItem { Content = action } : (object)null;
        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => value is ComboBoxItem comboBoxItem ? ( comboBoxItem.Content?.ToString() ) : (object)null;
    }

    public class EmptyCollectionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value is ICollection collection && collection.Count == 0 )
                return new List<string>() { string.Empty }; // Create a new collection with a default value

            return value;
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => throw new NotSupportedException();
    }

    public class ListToStringConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is IEnumerable list ) )
                return string.Empty;

            var serializedList = new StringBuilder();
            foreach ( object item in list
                .Cast<object>()
                .Where( item => item != null ) )
            {
                _ = serializedList.AppendLine( item.ToString() );
            }

            return serializedList.ToString();
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is string text ) )
                return new List<string>();

            string[] lines = text.Split( new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries );
            if ( targetType != typeof( List<Guid> ) )
                return lines.ToList();

            return lines.Select( line => Guid.TryParse( line, out Guid guid ) ? guid : Guid.Empty ).ToList();
        }
    }

    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) => value is bool isExpanded && targetType == typeof( string ) ? isExpanded ? "▼" : "▶" : (object)string.Empty;
        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => throw new NotSupportedException();
    }
}
