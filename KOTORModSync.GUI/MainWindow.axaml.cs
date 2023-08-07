// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.CallbackDialogs;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Component = KOTORModSync.Core.Component;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;

// ReSharper disable AsyncVoidMethod

namespace KOTORModSync
{
    [SuppressMessage( "ReSharper", "UnusedParameter.Local" )]
    public partial class MainWindow : Window
    {
        public static List<Component> ComponentsList => MainConfig.AllComponents;
        public new event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        [CanBeNull]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if ( _searchText == value )
                    return; // prevent recursion problems

                _searchText = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( SearchText ) ) );
            }
        }

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                InitializeControls();

                // Initialize the logger
                Logger.Initialize();

                // Create callback objects for use with KOTORModSync.Core
                CallbackObjects.SetCallbackObjects(
                    new ConfirmationDialogCallback( this ),
                    new OptionsDialogCallback( this )
                );

                PropertyChanged += SearchText_PropertyChanged;
            }
            catch ( Exception e )
            {
                Logger.LogException( e, "A fatal error has occurred loading the main window" );
                throw;
            }
        }

        public void InitializeControls()
        {
            if ( MainGrid.ColumnDefinitions == null || MainGrid.ColumnDefinitions.Count != 3 )
                throw new NullReferenceException( "MainGrid incorrectly defined, expected 3 columns." );

            // set title and version
            Title = $"KOTORModSync v{MainConfig.CurrentVersion}";
            TitleTextBlock.Text = Title;

            ColumnDefinition componentListColumn = MainGrid.ColumnDefinitions[0]
                ?? throw new NullReferenceException( "Column 0 of MainGrid (component list column) not defined." );
            ColumnDefinition configColumn = MainGrid.ColumnDefinitions[2]
                ?? throw new NullReferenceException( "Column 2 of MainGrid (component list column) not defined." );

            // Column 0
            componentListColumn.Width = new GridLength( 250 );

            // Column 1
            RawEditTextBox.LostFocus
                += RawEditTextBox_LostFocus; // Prevents RawEditTextBox from being cleared when clicking elsewhere(?)
            RawEditTextBox.DataContext = new ObservableCollection<string>();

            // Column 3
            configColumn.Width = new GridLength( 250 );
            MainConfigInstance = new MainConfig();

            MainConfigStackPanel.DataContext = MainConfigInstance;

            _ = Logger.LogVerboseAsync( "Setup window move event handlers..." );
            // Attach event handlers
            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;
            FindComboBoxesInWindow( this );
        }

        private bool _progressWindowClosed;
        private string _searchText;

        private MainConfig MainConfigInstance { get; set; }

        [CanBeNull] private Component _currentComponent;
        [CanBeNull] public Component CurrentComponent
        {
            get => _currentComponent;
            set => SetAndRaise( CurrentComponentProperty, ref _currentComponent, value );
        }

        public static readonly DirectProperty<MainWindow, Component> CurrentComponentProperty =
            AvaloniaProperty.RegisterDirect<MainWindow, Component>(
                nameof(CurrentComponent),
                o => o?.CurrentComponent,
                (o, v) => o.CurrentComponent = v
            );

        private bool _installRunning;

        private Window _outputWindow;
        private bool _ignoreWindowMoveWhenClickingComboBox;
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;

        private void SearchText_PropertyChanged( [NotNull] object sender, [NotNull] PropertyChangedEventArgs e )
        {
            if ( e.PropertyName != nameof( SearchText ) )
                return;

            // Get the root item of the TreeView
            var rootItem = (TreeViewItem)LeftTreeView.ContainerFromIndex(0);

            FilterControlListItems( rootItem, SearchText );
        }

        public static void FilterControlListItems( [NotNull] object item, [NotNull] string searchText )
        {
            if ( searchText == null )
                throw new ArgumentNullException( nameof( searchText ) );

            if ( !( item is Control controlItem ) )
                return; // no components loaded/created

            if ( controlItem.Tag is Component thisComponent )
                ApplySearchVisibility( controlItem, thisComponent.Name, searchText );

            // Iterate through the child items (TreeViewItem only)
            IEnumerable<ILogical> controlItemArray = controlItem.GetLogicalChildren();
            foreach ( TreeViewItem childItem in controlItemArray.OfType<TreeViewItem>() )
            {
                // Recursively filter the child item (TreeViewItem only)
                FilterControlListItems( childItem, searchText );
            }
        }

        private static void ApplySearchVisibility(
            [NotNull] Visual item,
            [NotNull] string itemName,
            [NotNull] string searchText
        )
        {
            if ( item is null )
                throw new ArgumentNullException( nameof( item ) );

            if ( itemName is null )
                throw new ArgumentNullException( nameof( itemName ) );

            if ( searchText is null )
                throw new ArgumentNullException( nameof( searchText ) );

            // Check if the item matches the search text
            // Show or hide the item based on the match
            item.IsVisible = itemName.IndexOf( searchText, StringComparison.OrdinalIgnoreCase ) >= 0;
        }

        // test the options dialog for use with the 'Options' IDictionary<string, object>.
        public async void Testwindow()
        {
            // Create an instance of OptionsDialogCallback
            var optionsDialogCallback = new OptionsDialogCallback( this );

            // Create a list of options
            var options = new List<string>
            {
                "Option 1", "Option 2", "Option 3",
            };

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

        // Prevents a combobox from dragging the window around.
        private void FindComboBoxes( [CanBeNull] Control control )
        {
            if ( !( control is ILogical visual ) )
                throw new ArgumentNullException( nameof( control ) );

            if ( control is ComboBox _ )
            {
                control.Tapped -= ComboBox_Opened;
                control.PointerCaptureLost -= ComboBox_Opened;
                control.Tapped += ComboBox_Opened;
                control.PointerCaptureLost += ComboBox_Opened;
            }

            if ( visual.LogicalChildren.IsNullOrEmptyOrAllNull() )
                return;

            // ReSharper disable once PossibleNullReferenceException
            foreach ( ILogical child in visual.LogicalChildren )
            {
                if ( child is Control childControl )
                {
                    FindComboBoxes( childControl );
                }
            }
        }

        public void FindComboBoxesInWindow( [NotNull] Window thisWindow )
        {
            if ( thisWindow is null )
                throw new ArgumentNullException( nameof( thisWindow ) );

            FindComboBoxes( thisWindow );
        }

        private void ComboBox_Opened( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            _mouseDownForWindowMoving = false;
            _ignoreWindowMoveWhenClickingComboBox = true;
        }

        private void InputElement_OnPointerMoved( [NotNull] object sender, [NotNull] PointerEventArgs e )
        {
            if ( !_mouseDownForWindowMoving )
                return;

            if ( _ignoreWindowMoveWhenClickingComboBox )
            {
                _ignoreWindowMoveWhenClickingComboBox = false;
                _mouseDownForWindowMoving = false;
                return;
            }

            PointerPoint currentPoint = e.GetCurrentPoint( this );
            Position = new PixelPoint(
                Position.X + (int)( currentPoint.Position.X - _originalPoint.Position.X ),
                Position.Y + (int)( currentPoint.Position.Y - _originalPoint.Position.Y )
            );
        }

        private void InputElement_OnPointerPressed( [NotNull] object sender, [NotNull] PointerEventArgs e )
        {
            if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
                return;

            if ( sender is ComboBox )
                return;

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint( this );
        }

        private void InputElement_OnPointerReleased( [NotNull] object sender, [NotNull] PointerEventArgs e ) =>
            _mouseDownForWindowMoving = false;
        [UsedImplicitly] private void CloseButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => Close();
        [UsedImplicitly] private void MinimizeButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) =>
            WindowState = WindowState.Minimized;

        [ItemCanBeNull]
        private async Task<string> OpenFile( List<FileDialogFilter> filters = null )
        {
            try
            {
                string[] result = await ShowFileDialog(
                    isFolderDialog: false,
                    filters
                );
                if ( result?.Length > 0 )
                    return result[0]; // Retrieve the first selected file path
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
                string[] filePaths = await ShowFileDialog( isFolderDialog: false, allowMultiple: true );
                if ( filePaths is null )
                {
                    await Logger.LogVerboseAsync( "User did not select any files." );
                    return null;
                }

                await Logger.LogVerboseAsync( $"Selected files: [{string.Join( $",{Environment.NewLine}", filePaths )}]" );
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
                string[] thisFolder = await ShowFileDialog( isFolderDialog: true, filters: null );
                return thisFolder?[0];
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
                return null;
            }
        }

        [ItemCanBeNull]
        private async Task<string> SaveFile( string saveWindowTitle = null, [CanBeNull] List<string> defaultExt = null )
        {
            try
            {
                if ( defaultExt is null )
                {
                    defaultExt = new List<string>
                    {
                        "toml", "tml",
                    };
                }

                var dialog = new SaveFileDialog
                {
                    DefaultExtension = defaultExt.FirstOrDefault(),
                    Filters =
                    {
                        new FileDialogFilter
                        {
                            Name = "All Files",
                            Extensions = { "*" },
                        },
                        new FileDialogFilter
                        {
                            Name = "Preferred Extensions",
                            Extensions = defaultExt,
                        },
                    },
                };
                if ( !string.IsNullOrEmpty( saveWindowTitle ) )
                    dialog.Title = saveWindowTitle;

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
            [CanBeNull] List<FileDialogFilter> filters = null,
            bool allowMultiple = false,
            string startFolder = null
        )
        {
            try
            {
                if ( !( VisualRoot is Window parent ) )
                {
                    await Logger.LogErrorAsync(
                        $"Could not open {( isFolderDialog ? "folder" : "file" )} dialog - parent window not found"
                    );
                    return default;
                }

                string[] results;
                if ( isFolderDialog )
                {
                    var folderDialog = new OpenFolderDialog();
                    if ( !string.IsNullOrEmpty( startFolder ) )
                        folderDialog.Directory = startFolder;

                    results = new[]
                    {
                        await folderDialog.ShowAsync( parent ),
                    };
                }
                else
                {
                    var fileDialog = new OpenFileDialog
                    {
                        AllowMultiple = allowMultiple,
                    };
                    if ( filters != null )
                    {
                        fileDialog.Filters = new List<FileDialogFilter>
                        {
                            new FileDialogFilter
                            {
                                Name = "All Files",
                                Extensions = { "*" },
                            },
                        };
                    }
                    if ( !string.IsNullOrEmpty( startFolder ) )
                        fileDialog.Directory = startFolder;

                    results = await fileDialog.ShowAsync( parent );
                }

                if ( results is null || results.Length == 0 )
                {
                    await Logger.LogVerboseAsync( "User did not make a selection" );
                    return default;
                }

                await Logger.LogAsync(
                    $"Selected {( isFolderDialog ? "folder" : "file" )}: {string.Join( separator: ", ", results )}"
                );
                return results;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }

            return null;
        }

        private async Task<bool> FindDuplicateComponents( [NotNull][ItemNotNull] List<Component> components )
        {
            if ( components == null )
                throw new ArgumentNullException( nameof( components ) );

            // Check for duplicate GUID
            bool duplicatesFixed = true;
            bool promptUser = true;
            foreach ( Component component in components )
            {
                Component duplicateComponent
                    = components.Find( c => c.Guid == component.Guid && c != component );

                if ( duplicateComponent is null )
                    continue;

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

                bool? confirm = true;
                if ( promptUser )
                {
                    confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        parentWindow: this,
                        confirmText: $"{message}.{Environment.NewLine}Assign a random GUID to '{duplicateComponent.Name}'? (default: NO)"
                    );
                }

                switch ( confirm )
                {
                    case true:
                        duplicateComponent.Guid = Guid.NewGuid();
                        _ = Logger.LogAsync( $"Replaced GUID of component '{duplicateComponent.Name}'" );
                        break;
                    case false:
                        _ = Logger.LogVerboseAsync(
                            $"User canceled GUID replacement for component '{duplicateComponent.Name}'"
                        );
                        duplicatesFixed = false;
                        break;
                    case null:
                        promptUser = false;
                        break;
                }
            }

            return duplicatesFixed;
        }

        [UsedImplicitly]
        private async void LoadInstallFile_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                // Extension filters for our instruction file
                var filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Name = "Mod Sync File",
                        Extensions = { "toml", "tml" },
                    },
                    new FileDialogFilter
                    {
                        Name = "All Files",
                        Extensions = { "*" },
                    },
                };

                // Open the file dialog to select a file
                string filePath = await OpenFile(filters);
                if ( !PathValidator.IsValidPath( filePath ) )
                    return;

                var thisFile = new FileInfo( filePath );

                // Verify the file type
                string fileExtension = thisFile.Extension;
                const int maxInstructionSize = 524288000; // instruction file larger than 500mb is probably unsupported
                if (
                    !new List<string>{".toml", ".tml", ".txt"}.Contains(
                        fileExtension,
                        StringComparer.OrdinalIgnoreCase
                    )
                    ^ thisFile.Length > maxInstructionSize
                )
                {
                    _ = Logger.LogAsync( $"Invalid extension for file '{thisFile.Name}'" );
                    return;
                }

                if ( MainConfig.AllComponents.Count > 0 )
                {
                    bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                        parentWindow: this,
                        confirmText: "You already have a config loaded. Do you want to load this instruction file anyway?"
                    );
                    if ( confirm != true )
                        return;
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
                    return; // user cancelled

                using ( var reader = new StreamReader( filePath ) )
                {
                    string fileContents = await reader.ReadToEndAsync();
                    if ( MainConfig.AllComponents.Count > 0
                        && await ConfirmationDialog.ShowConfirmationDialog(
                            parentWindow: this,
                            confirmText: "You already have a config loaded. Do you want to load the markdown anyway?"
                        )
                        != true )
                    {
                        return;
                    }

                    List<Component> parsedMods = ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) )
                        ?? throw new NullReferenceException( "ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) )" );

                    MainConfigInstance.allComponents = parsedMods;
                    await ProcessComponentsAsync( MainConfig.AllComponents );
                }
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private void OpenLink_Click( [NotNull] object sender, [NotNull] TappedEventArgs e )
        {
            if ( !( sender is TextBlock textBlock ) )
                return;

            try
            {
                string url = textBlock.Text ?? string.Empty;
                if ( !Uri.TryCreate( url, UriKind.Absolute, out Uri _ ) )
                    throw new ArgumentException( "Invalid URL" );

                if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                {
                    _ = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = url, UseShellExecute = true,
                        }
                    );
                }
                else if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
                {
                    _ = Process.Start( fileName: "open", url );
                }
                else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) )
                {
                    _ = Process.Start( fileName: "xdg-open", url );
                }
                else
                {
                    Logger.LogError( "Unsupported platform, cannot open link." );
                }
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, $"Failed to open URL: {ex.Message}" );
            }
        }

        [UsedImplicitly]
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
                        $"Invalid files found. Please report this issue to the developer: [{string.Join( separator: ",", files )}]"
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
                        parameter: null,
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

        [UsedImplicitly]
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
                    destinationTextBox.Text = thisInstruction.Destination;
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        [UsedImplicitly]
        private async void SaveButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        message: "Please select a component from the list or create a new one before saving."
                    );
                    return;
                }

                await Logger.LogVerboseAsync( $"Selected '{CurrentComponent.Name}'" );

                if ( !await ShouldSaveChanges() )
                    return;

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
                if (
                    MainConfigInstance is null
                    || MainConfig.DestinationPath is null
                    || MainConfig.SourcePath is null
                )
                {
                    return ( false, "Please set your directories first" );
                }

                if ( MainConfig.AllComponents.IsNullOrEmptyCollection() )
                {
                    return ( false,
                        "No instructions loaded! Press 'Load Instructions File' or create some instructions first." );
                }

                await Logger.LogAsync( "Finding duplicate case-insensitive folders/files in the install destination..." );
                List<FileSystemInfo> duplicates = PathHelper.FindCaseInsensitiveDuplicates( MainConfig.DestinationPath.FullName );
                foreach ( FileSystemInfo duplicate in duplicates )
                {
                    await Logger.LogErrorAsync( duplicate?.FullName + " has a duplicate, please resolve before attempting an install." );
                }

                await Logger.LogAsync( "Checking for duplicate components..." );
                bool noDuplicateComponents = await FindDuplicateComponents( MainConfig.AllComponents );

                // Ensure necessary directories are writable.
                await Logger.LogAsync( "Ensuring both the mod directory and the install directory are writable..." );
                bool isInstallDirectoryWritable = Utility.IsDirectoryWritable( MainConfig.DestinationPath );
                bool isModDirectoryWritable = Utility.IsDirectoryWritable( MainConfig.SourcePath );

                await Logger.LogAsync( "Validating the order of operations and install order of all components..." );
                ( bool isCorrectOrder, List<Component> reorderedList )
                    = Component.ConfirmComponentsInstallOrder( MainConfig.AllComponents );
                if ( !isCorrectOrder && MainConfig.AttemptFixes )
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
                        continue;

                    if ( component.Restrictions.Count > 0 && component.IsSelected )
                    {
                        List<Component> restrictedComponentsList = Component.FindComponentsFromGuidList(
                            component.Restrictions,
                            MainConfig.AllComponents
                        );
                        foreach ( Component restrictedComponent in restrictedComponentsList )
                        {
                            // ReSharper disable once InvertIf
                            if ( restrictedComponent?.IsSelected == true )
                            {
                                await Logger.LogErrorAsync(
                                    $"Cannot install '{component.Name}' due to '{restrictedComponent.Name}' being selected for install."
                                );
                                individuallyValidated = false;
                            }
                        }
                    }

                    if ( component.Dependencies.Count > 0 && component.IsSelected )
                    {
                        List<Component> dependencyComponentsList = Component.FindComponentsFromGuidList(
                            component.Dependencies,
                            MainConfig.AllComponents
                        );
                        foreach ( Component dependencyComponent in dependencyComponentsList )
                        {
                            // ReSharper disable once InvertIf
                            if ( dependencyComponent?.IsSelected != true )
                            {
                                await Logger.LogErrorAsync(
                                    $"Cannot install '{component.Name}' due to '{dependencyComponent?.Name}' not being selected for install."
                                );
                                individuallyValidated = false;
                            }
                        }
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


                if ( duplicates.Any() )
                {
                    informationMessage =
                        "You have duplicate files/folders in your installation directory in a case-insensitive environment."
                        + "Please resolve these before continuing. Check the output window for the specific files to resolve.";
                }

                if ( !informationMessage.Equals( string.Empty ) )
                    return ( false, informationMessage );

                return (
                    true,
                    "No issues found. If you encounter any problems during the installation, please contact the developer."
                );

            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
                return ( false, "Unknown error, check the output window for more information." );
            }
        }

        [UsedImplicitly]
        private async void ValidateButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                ( bool success, string informationMessage ) = await PreinstallValidation();
                await InformationDialog.ShowInformationDialog( this, informationMessage );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        [UsedImplicitly]
        private async void AddComponentButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            // Create a new default component with a new GUID
            try
            {
                var newComponent = new Component();

                newComponent.Guid = Guid.NewGuid();
                newComponent.Name = "new mod_" + Path.GetFileNameWithoutExtension( Path.GetRandomFileName() );
                // Add the new component to the collection
                MainConfigInstance.allComponents.Add( newComponent );

                // Load into the editor
                LoadComponentDetails( newComponent );

                // Refresh the TreeView to reflect the changes
                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        [UsedImplicitly]
        private async void RefreshComponents_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) =>
            await ProcessComponentsAsync( MainConfig.AllComponents );

        [UsedImplicitly]
        private async void RemoveComponentButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            // Get the selected component from the TreeView
            try
            {
                if ( CurrentComponent is null )
                {
                    Logger.Log( "No component loaded into editor - nothing to remove." );
                    return;
                }

                // todo:
                if ( MainConfig.AllComponents.Any(
                        c => c.Dependencies.Any( g => g == CurrentComponent.Guid )
                    )
                )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        $"Cannot remove '{CurrentComponent.Name}', there are several components that rely on it. Please address this problem first."
                    );
                    return;
                }

                // Remove the selected component from the collection
                _ = MainConfigInstance.allComponents.Remove( CurrentComponent );
                SetCurrentComponent( null );

                // Refresh the TreeView to reflect the changes
                await ProcessComponentsAsync( MainConfig.AllComponents );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        [UsedImplicitly]
        private async void SetDirectories_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                await InformationDialog.ShowInformationDialog(
                    this,
                    message:
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
                    message: "Please select your mod directory (where ALL your mods are downloaded)."
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
                await Logger.LogExceptionAsync( ex, customMessage: "Unknown error - please report to a developer" );
            }
        }

        [UsedImplicitly]
        private async void InstallModSingle_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        message: "There's already another installation running, please check the output window."
                    );
                    return;
                }

                if ( MainConfigInstance is null || MainConfig.DestinationPath is null )
                {
                    var informationDialog = new InformationDialog
                    {
                        InfoText = "Please set your directories first",
                    };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                if ( CurrentComponent is null )
                {
                    var informationDialog = new InformationDialog
                    {
                        InfoText = "Please choose a mod to install from the left list first",
                    };
                    _ = await informationDialog.ShowDialog<bool?>( this );
                    return;
                }

                string name = CurrentComponent.Name; // use correct name even if user clicks another component.

                bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
                    this,
                    CurrentComponent.Directions
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Press Yes to execute the provided directions now."
                );
                if ( confirm != true )
                {
                    await Logger.LogAsync( $"User cancelled install of '{name}'" );
                    return;
                }

                var validator = new ComponentValidation( CurrentComponent, MainConfig.AllComponents );
                await Logger.LogVerboseAsync( $" == Validating '{name}' == " );
                if ( !validator.Run() )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        message: "This component could not be validated, please check the output window."
                    );
                    return;
                }

                try
                {
                    _installRunning = true;

                    Component.InstallExitCode exitCode = await Task.Run(
                        () => CurrentComponent.InstallAsync( MainConfig.AllComponents )
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

        [UsedImplicitly]
        private async void StartInstall_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( _installRunning )
                {
                    await InformationDialog.ShowInformationDialog(
                        this,
                        message: "There's already an installation running, please check the output window."
                    );
                    return;
                }

                ( bool success, string informationMessage ) = await PreinstallValidation();
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

                if ( await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        confirmText: "Really install all mods?"
                    )
                    != true
                )
                {
                    return;
                }

                try
                {
                    _ = Logger.LogAsync( "Start installing all mods..." );
                    _installRunning = true;

                    var progressWindow = new ProgressWindow
                    {
                        ProgressBar = { Value = 0 }
                    };
                    progressWindow.Closed += ProgressWindowClosed;
                    progressWindow.Show();
                    _progressWindowClosed = false;

                    Component.InstallExitCode exitCode = Component.InstallExitCode.UnknownError;

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
                                await Task.Delay( millisecondsDelay: 100 ); // Introduce a small delay
                                await Dispatcher.UIThread.InvokeAsync(
                                    () => { }
                                ); // Invoke an empty action to ensure UI updates are processed
                                await Task.Delay( millisecondsDelay: 50 ); // Introduce another small delay
                            }
                        );

                        // Ensure the UI updates are processed
                        await Task.Yield();
                        await Task.Delay( millisecondsDelay: 200 );

                        if ( !component.IsSelected )
                        {
                            await Logger.LogAsync( $"Skipping install of '{component.Name}' (unchecked)" );
                            continue;
                        }

                        await Logger.LogAsync( $"Start Install of '{component.Name}'..." );
                        exitCode = await component.InstallAsync( MainConfig.AllComponents );
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

                    if ( exitCode == Component.InstallExitCode.Success)
                        await InformationDialog.ShowInformationDialog( this, "All components successfully installed!" );

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
            try
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
            catch ( Exception exception )
            {
                Logger.LogException( exception );
            }
        }

        [UsedImplicitly]
        private async void DocsButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                string file = await SaveFile(
                    "Save instructions documentation to file",
                    new List<string>{ "txt" }
                );
                if ( file is null )
                    return; // user cancelled

                string docs = Component.GenerateModDocumentation( MainConfig.AllComponents );
                await SaveDocsToFileAsync( file, docs );
                string message = $"Saved documentation of {MainConfig.AllComponents.Count} mods to '{file}'";
                await InformationDialog.ShowInformationDialog( this, message );
                _ = Logger.LogAsync( message );
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex, customMessage: "Error generating and saving documentation" );
                await InformationDialog.ShowInformationDialog(
                    this,
                    message: "An unexpected error occurred while generating and saving documentation."
                );
            }
        }

        private static async Task SaveDocsToFileAsync( [NotNull] string filePath, [NotNull] string documentation )
        {
            if ( filePath is null )
                throw new ArgumentNullException( nameof( filePath ) );
            if ( documentation is null )
                throw new ArgumentNullException( nameof( documentation ) );

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

        /// <summary>
        /// Event handler for the TabControl's SelectionChanged event.
        /// This method manages tab selection changes in the TabControl and performs various actions based on the user's interaction.
        /// When the user selects a different tab, the method first checks if an internal tab change is being ignored. If so, it immediately returns without performing any further actions.
        /// Additionally, this method relies on a component being currently loaded for proper operation. If no component is loaded, the method will log a verbose
        /// message, indicating that the tab functionality won't work until a component is loaded.
        /// 
        /// The method identifies the last selected tab and the newly selected tab and logs their headers to provide user feedback about their selections.
        /// However, it assumes that the TabControl's SelectionChanged event arguments will always have valid items.
        /// If not, it will log a verbose message, indicating that it couldn't resolve the tab item.
        /// 
        /// **Caution**: The method tries to resolve the names of the tabs based on their headers, and it assumes that this information will be available.
        /// If any tab lacks a header, it may lead to unexpected behavior or errors.
        /// 
        /// If there are no components in the MainConfig or the current component is null, the method defaults to the initial tab and logs a verbose message.
        /// However, the conditions under which a component is considered "null" or whether the MainConfig contains any valid components are not explicitly detailed in this method.
        /// 
        /// The method then compares the names of the current and last selected tabs in lowercase to detect if the user clicked on the same tab.
        /// If so, it logs a message and returns without performing any further actions.
        /// 
        /// **Warning**: The logic in this method may trigger swapping of tabs based on certain conditions, such as selecting the "raw edit" tab or changing from the "raw edit" tab to another.
        /// It is important to be aware of these tab-swapping behaviors to avoid unexpected changes in the user interface.
        /// 
        /// The method determines whether the tab should be swapped based on the selected tab's name.
        /// If the new tab is "raw edit", it calls the LoadIntoRawEditTextBox method to check if the current component should be loaded into the raw editor.
        /// The specific criteria for loading a component into the raw editor are not detailed within this method.
        /// 
        /// If the last tab was "raw edit", the method checks if changes should be saved before swapping to the new tab.
        /// The method finally decides whether to prevent the tab change and returns accordingly.
        /// Depending on the conditions mentioned earlier, tab swapping may be cancelled, which might not be immediately apparent to the user.
        /// 
        /// Furthermore, this method modifies the visibility of certain UI elements (RawEditTextBox and ApplyEditorButton) based on the selected tab.
        /// Specifically, it shows or hides these elements when the "raw edit" tab is selected, which could impact user interactions if not understood properly.
        /// </summary>
        /// <param name="sender">The object that raised the event (expected to be a TabControl).</param>
        /// <param name="e">The event arguments containing information about the selection change.</param>
        [UsedImplicitly]
        private async void TabControl_SelectionChanged( [NotNull] object sender, [NotNull] SelectionChangedEventArgs e )
        {
            if ( _ignoreInternalTabChange )
                return;

            try
            {
                if ( !( sender is TabControl tabControl ) )
                {
                    await Logger.LogErrorAsync( "Sender is not a TabControl control" );
                    return;
                }

                if ( CurrentComponent is null )
                {
                    await Logger.LogVerboseAsync( "No component loaded, tabs can't be used until one is loaded first." );
                    SetTabInternal(tabControl, InitialTab);
                    return;
                }

                // Get the last selected TabItem
                // ReSharper disable once PossibleNullReferenceException
                if ( e.RemovedItems.IsNullOrEmptyOrAllNull() || !( e.RemovedItems[0] is TabItem lastSelectedTabItem ) )
                {
                    await Logger.LogVerboseAsync(
                        "Previous tab item could not be resolved somehow?"
                    );
                    return;
                }
                await Logger.LogVerboseAsync($"User is attempting to swap from: {lastSelectedTabItem.Header}");

                // Get the new selected TabItem
                // ReSharper disable once PossibleNullReferenceException
                if ( e.AddedItems.IsNullOrEmptyOrAllNull() || !( e.AddedItems[0] is TabItem attemptedTabSelection ) )
                {
                    await Logger.LogVerboseAsync(
                        "Attempted tab item could not be resolved somehow?"
                    );
                    return;
                }
                await Logger.LogVerboseAsync($"User is attempting to swap to: {attemptedTabSelection.Header}");

                // Don't show content of any tabs (except the hidden one) if there's no content.
                if ( MainConfig.AllComponents.IsNullOrEmptyCollection() || CurrentComponent is null )
                {
                    SetTabInternal(tabControl, InitialTab);
                    await Logger.LogVerboseAsync( "No config loaded, defaulting to initial tab." );
                    return;
                }

                string tabName = GetControlNameFromHeader( attemptedTabSelection )?
                    .ToLowerInvariant();
                string lastTabName = GetControlNameFromHeader( lastSelectedTabItem )?
                    .ToLowerInvariant();

                // do nothing if clicking the same tab
                if ( tabName == lastTabName )
                {
                    await Logger.LogVerboseAsync( $"Selected tab is already the current tab '{tabName}'" );
                    return;
                }


                bool shouldSwapTabs = true;
                if ( tabName == "raw edit" )
                {
                    shouldSwapTabs = await LoadIntoRawEditTextBox( CurrentComponent );
                }
                else if ( lastTabName == "raw edit" )
                {
                    shouldSwapTabs = await ShouldSaveChanges( true );
                    if ( shouldSwapTabs )
                    {
                        // unload the raw editor
                        RawEditTextBox.Text = string.Empty;
                    }
                }

                // Prevent the attempted tab change
                if ( !shouldSwapTabs )
                {
                    SetTabInternal(tabControl, lastSelectedTabItem);
                    return;
                }

                // Show/hide the appropriate content based on the selected tab
                RawEditTextBox.IsVisible = tabName == "raw edit";
                ApplyEditorButton.IsVisible = tabName == "raw edit";
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Get the ComboBox
                if ( !( sender is ComboBox comboBox ) )
                {
                    Logger.Log( "Sender is not a ComboBox." );
                    return;
                }

                // Get the instruction
                if ( !( comboBox.DataContext is Instruction thisInstruction ) )
                {
                    Logger.Log( "ComboBox's DataContext must be an instruction for this method." );
                    return;
                }

                // Get the selected item
                string selectedItem = comboBox.SelectedItem as string;

                // Convert Items to a List<string> and find the index
                var itemsList = comboBox.Items.Cast<string>().ToList();
                int index = itemsList.IndexOf(selectedItem);

                // Assign to instruction.
                thisInstruction.Arguments = index.ToString();
                thisInstruction.Action = Instruction.ActionType.TSLPatcher;
            }
            catch ( Exception exception )
            {
                Logger.LogException( exception );
            }
        }

        [CanBeNull]
        private TabItem GetCurrentTabItem( [CanBeNull] TabControl tabControl ) => (tabControl ?? TabControl)?.SelectedItem as TabItem;

        [CanBeNull]
        private static string GetControlNameFromHeader( [CanBeNull] TabItem tabItem ) => tabItem?.Header?.ToString();
        private bool _ignoreInternalTabChange { get; set; }

        private void SetTabInternal( [NotNull] TabControl tabControl, TabItem tabItem )
        {
            if ( tabControl is null )
                throw new ArgumentNullException( nameof( tabControl ) );

            _ignoreInternalTabChange = true;
            tabControl.SelectedItem = tabItem;
            _ignoreInternalTabChange = false;
        }

        private async void LoadComponentDetails( [NotNull] Component selectedComponent )
        {
            if ( selectedComponent == null )
                throw new ArgumentNullException( nameof( selectedComponent ) );

            bool confirmLoadOverwrite = true;
            if ( GetControlNameFromHeader( GetCurrentTabItem(TabControl) )?
                    .ToLowerInvariant() == "raw edit" )
            {
                confirmLoadOverwrite = await LoadIntoRawEditTextBox( selectedComponent );
            }
            else if ( selectedComponent != CurrentComponent )
            {
                confirmLoadOverwrite = await ShouldSaveChanges();
            }

            if ( !confirmLoadOverwrite )
                return;

            // set the currently tracked component to what's being loaded.
            SetCurrentComponent( selectedComponent );

            // default to SummaryTabItem.
            if ( InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue )
            {
                SetTabInternal(TabControl, SummaryTabItem);
            }
        }

        private void SetCurrentComponent( [CanBeNull] Component c ) => CurrentComponent = c;

        private async Task<bool> LoadIntoRawEditTextBox( [NotNull] Component selectedComponent )
        {
            if ( selectedComponent is null )
                throw new ArgumentNullException( nameof( selectedComponent ) );

            _ = Logger.LogVerboseAsync( $"Loading '{selectedComponent.Name}' into the raw editor..." );
            if ( CurrentComponentHasChanges() && CurrentComponent != selectedComponent )
            {
                bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
                    parentWindow: this,
                    confirmText: "You're attempting to load the component into the raw editor, but"
                    + " there may be unsaved changes still in the editor. Really continue?"
                );

                // double check with user before overwrite
                if ( confirmResult != true )
                    return false;
            }

            // populate raw editor
            RawEditTextBox.Text = selectedComponent.SerializeComponent();

            return true;
        }

        // todo: figure out if this is needed.
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void RawEditTextBox_LostFocus( [NotNull] object sender, [NotNull] RoutedEventArgs e ) =>
            e.Handled = true;

        private bool CurrentComponentHasChanges()
        {
            if ( CurrentComponent == null )
                return false;

            if ( string.IsNullOrWhiteSpace( RawEditTextBox.Text ) )
                return false;

            return RawEditTextBox.Text != CurrentComponent.SerializeComponent();
        }

        /// <summary>
        /// Asynchronous method that determines if changes should be saved before performing an action.
        /// This method checks if the current component has any changes and prompts the user for confirmation if necessary.
        /// 
        /// The method attempts to deserialize the raw config text from the "RawEditTextBox" into a new Component instance.
        /// If the deserialization process fails due to syntax errors, it will display a confirmation dialog to the user despite the 'noPrompt' boolean,
        /// offering to discard the changes and continue with the last attempted action. If the user chooses to discard,
        /// the method returns true, indicating that the changes should not be saved.
        /// 
        /// The method then tries to find the corresponding component in the "MainConfig.AllComponents" collection.
        /// If the index of the current component cannot be found or is out of range, the method logs an error,
        /// displays an information dialog to the user, and returns false, indicating that the changes cannot be saved.
        /// 
        /// If all checks pass successfully, the method updates the properties of the component in the "MainConfig.AllComponents" collection
        /// with the deserialized new component, sets the current component to the new one, and refreshes the tree view to reflect the changes.
        /// 
        /// **Note**: This method involves multiple asynchronous operations and may not complete immediately.
        /// Any unexpected exceptions that occur during the process are caught, logged, and displayed to the user via an information dialog.
        /// 
        /// </summary>
        /// <param name="noPrompt">A boolean flag indicating whether the user should be prompted to save changes. Default is false.</param>
        /// <returns>True if the changes should be saved or if no changes are detected. False if the user chooses not to save or if an error occurs.</returns>
        private async Task<bool> ShouldSaveChanges( bool noPrompt = false )
        {
            string output;
            try
            {
                if ( !CurrentComponentHasChanges() )
                {
                    await Logger.LogVerboseAsync( "No changes detected, ergo nothing to save." );
                    return true;
                }

                if (
                    !noPrompt
                    && await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        confirmText: "Are you sure you want to save?"
                    ) != true
                )
                {
                    return false;
                }

                // Get the selected component from the tree view
                if ( CurrentComponent is null )
                {
                    output = "CurrentComponent is null which shouldn't ever happen in this context."
                        + Environment.NewLine
                        + "Please report this issue to a developer, this should never happen.";

                    await Logger.LogErrorAsync( output );
                    await InformationDialog.ShowInformationDialog( this, output );
                    return false;
                }

                var newComponent = Component.DeserializeTomlComponent( RawEditTextBox.Text );
                if ( newComponent is null )
                {
                    bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
                        this,
                        "Could not deserialize your raw config text into a Component instance in memory."
                        + " There may be syntax errors, check the output window for details."
                        + Environment.NewLine + Environment.NewLine
                        + "Would you like to discard your changes and continue with your last attempted action?"
                    );

                    return confirmResult == true;
                }

                // Find the corresponding component in the collection
                int index = MainConfig.AllComponents.IndexOf( CurrentComponent );
                if ( index < 0 || index >= MainConfig.AllComponents.Count )
                {
                    string componentName = string.IsNullOrWhiteSpace( newComponent?.Name )
                        ? "."
                        : $" '{newComponent.Name}'.";
                    output = $"Could not find the index of component{componentName}"
                        + " Ensure you single-clicked on a component on the left before pressing save."
                        + " Please back up your work and try again.";
                    await Logger.LogErrorAsync( output );
                    await InformationDialog.ShowInformationDialog(
                        this,
                        output
                    );

                    return false;
                }

                // Update the properties of the component
                MainConfigInstance.allComponents[index] = newComponent;
                SetCurrentComponent( newComponent );

                // Refresh the tree view to reflect the changes
                await ProcessComponentsAsync( MainConfig.AllComponents );
                await Logger.LogAsync(
                    $"Saved '{newComponent.Name}' successfully. Refer to the output window for more information."
                );
                return true;
            }
            catch ( Exception ex )
            {
                output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
                await Logger.LogExceptionAsync( ex );
                await InformationDialog.ShowInformationDialog(
                    this,
                    ex.Message + Environment.NewLine + output
                );
                return false;
            }
        }

        private async void MoveComponentListItem( [CanBeNull] Control selectedTreeViewItem, int relativeIndex )
        {
            try
            {
                var treeViewComponent = (Component)selectedTreeViewItem?.Tag;

                int index = MainConfig.AllComponents.IndexOf( treeViewComponent );
                if ( treeViewComponent is null
                    || index == 0 && relativeIndex < 0
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

        [UsedImplicitly]
        private void MoveUpButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            if ( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
            {
                MoveComponentListItem( selectedTreeViewItem, relativeIndex: -1 );
            }
        }

        [UsedImplicitly]
        private void MoveDownButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            if ( LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem )
            {
                MoveComponentListItem( selectedTreeViewItem, relativeIndex: 1 );
            }
        }

        [UsedImplicitly]
        private async void SaveModFile_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                string filePath = await SaveFile(saveWindowTitle: "Save instructions config Tomlin");
                if ( filePath is null )
                    return;

                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>()
                    .FirstOrDefault();
                if ( rootItem is null )
                    return;

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

        [UsedImplicitly]
        private void GenerateGuidButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                GuidGeneratedTextBox.Text = Guid.NewGuid().ToString();
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex );
            }
        }


        private void ComponentCheckboxChecked(
            [NotNull] Component component,
            [NotNull] HashSet<Component> visitedComponents,
            bool suppressErrors = false
        )
        {
            if ( component is null )
                throw new ArgumentNullException( nameof( component ) );
            if ( visitedComponents is null )
                throw new ArgumentNullException( nameof( visitedComponents ) );

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
                if ( conflicts.TryGetValue( key: "Dependency", out List<Component> dependencyConflicts ) )
                {
                    foreach ( Component conflictComponent in dependencyConflicts )
                    {
                        // ReSharper disable once InvertIf
                        if ( conflictComponent?.IsSelected == false )
                        {
                            conflictComponent.IsSelected = true;
                            ComponentCheckboxChecked( conflictComponent, visitedComponents );
                        }
                    }
                }

                if ( conflicts.TryGetValue( key: "Restriction", out List<Component> restrictionConflicts ) )
                {
                    foreach ( Component conflictComponent in restrictionConflicts )
                    {
                        // ReSharper disable once InvertIf
                        if ( conflictComponent?.IsSelected == true )
                        {
                            conflictComponent.IsSelected = false;
                            ComponentCheckboxUnchecked( conflictComponent, visitedComponents );
                        }
                    }
                }

                // Handling OTHER component's defined restrictions based on the change to THIS component.
                foreach ( Component c in MainConfig.AllComponents )
                {
                    if ( !c.IsSelected || !c.Restrictions.Contains( component.Guid ) )
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

        private void ComponentCheckboxUnchecked(
            [NotNull] Component component,
            [CanBeNull] HashSet<Component> visitedComponents,
            bool suppressErrors = false
        )
        {
            if ( component is null )
                throw new ArgumentNullException( nameof( component ) );

            visitedComponents = visitedComponents ?? new HashSet<Component>();
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
                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>()
                    .FirstOrDefault();
                if ( rootItem != null )
                {
                    DockPanel headerPanel = (DockPanel)rootItem.Header
                        ?? throw new InvalidCastException( "Your TreeView isn't supported: header must be wrapped by top-level DockPanel" );
                    CheckBox checkBox = headerPanel.Children?.OfType<CheckBox>()
                        .FirstOrDefault();

                    if ( checkBox != null && !suppressErrors )
                    {
                        checkBox.IsChecked = null;
                    }
                }

                // Add the component to the visited set
                _ = visitedComponents.Add( component );

                // Handling OTHER component's defined dependencies based on the change to THIS component.
                foreach ( Component c in MainConfig.AllComponents )
                {
                    if ( c.IsSelected && c.Dependencies.Contains( component.Guid ) )
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

        // Set up the event handler for the checkbox
        void OnCheckBoxOnChecked( object sender, RoutedEventArgs e )
        {
            if ( !( sender is CheckBox checkBox ) )
                return;

            if ( !( checkBox.Tag is Component thisComponent ) )
                return;

            ComponentCheckboxChecked( thisComponent, new HashSet<Component>() );
        }

        
        void OnCheckBoxOnUnchecked( object sender, RoutedEventArgs e )
        {
            if ( !( sender is CheckBox checkBox ) )
                return;

            if ( !( checkBox.Tag is Component thisComponent ) )
                return;

            ComponentCheckboxUnchecked( thisComponent, new HashSet<Component>() );
        }

        [NotNull]
        private CheckBox CreateComponentCheckbox( [NotNull] Component component )
        {
            if ( component is null )
                throw new ArgumentNullException( nameof( component ) );

            var checkBox = new CheckBox
            {
                Name = "IsSelected",
                IsChecked = true,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = component,
            };
            var binding = new Binding( "IsSelected" )
            {
                Source = component,
                Mode = BindingMode.TwoWay,
            };

            checkBox.Checked += OnCheckBoxOnChecked;
            checkBox.Unchecked += OnCheckBoxOnUnchecked;

            if ( ToggleButton.IsCheckedProperty != null )
            {
                _ = checkBox.Bind( ToggleButton.IsCheckedProperty, binding );
            }

            return checkBox;
        }

        [NotNull]
        private Control CreateComponentHeader([NotNull] Component component, int index)
        {
            if (component is null)
                throw new ArgumentNullException(nameof(component));

            CheckBox checkBox = CreateComponentCheckbox(component);

            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(0, GridUnitType.Auto),
                    new ColumnDefinition(0, GridUnitType.Auto),
                    new ColumnDefinition(1, GridUnitType.Star),
                },
            };
    
            header.Children.Add(checkBox);
            Grid.SetColumn(checkBox, 0);

            var indexTextBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Text = $"{index + 1}: ",
                FontWeight = FontWeight.DemiBold,
                Margin = new Thickness(left: 0, top: 0, right: 5, bottom: 0),
            };
            header.Children.Add(indexTextBlock);
            Grid.SetColumn(indexTextBlock, 1);

            var nameTextBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center, 
                Text = $"{component.Name}",
                Focusable = false,
            };
            header.Children.Add(nameTextBlock);
            Grid.SetColumn(nameTextBlock, 2);

            return header;
        }



        private TreeViewItem CreateComponentItem( [NotNull] Component component, int index )
        {
            if ( component is null )
                throw new ArgumentNullException( nameof( component ) );

            var componentItem = new TreeViewItem
            {
                Header = CreateComponentHeader( component, index ),
                Tag = component,
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            componentItem.PointerPressed += ( sender, e ) =>
            {
                try
                {
                    ItemClickCommand?.Execute( component );
                    // ReSharper disable once PossibleNullReferenceException
                    e.Handled = true; // Prevent event bubbling
                }
                catch ( Exception exception )
                {
                    Logger.LogException( exception );
                }
            };

            return componentItem;
        }

        [CanBeNull]
        private static TreeViewItem FindExistingItem(
            [CanBeNull] ItemsControl parentItem,
            [CanBeNull] Component component
        )
        {
            if ( !( parentItem?.Items is IEnumerable items ) )
                return null;

            foreach ( object item in items )
            {
                if ( !( item is TreeViewItem treeViewItem ) )
                    continue;

                if ( treeViewItem.Tag is Component treeViewComponent && treeViewComponent.Equals( component ) )
                    return treeViewItem;
            }

            return null;
        }

        private void CreateTreeViewItem( [NotNull] Component component, ItemsControl parentItem, int index )
        {
            try
            {
                if ( parentItem is null )
                    throw new ArgumentNullException( nameof( parentItem ) );

                if ( component is null )
                    throw new ArgumentNullException( nameof( component ) );

                if ( !( parentItem.ItemsSource is AvaloniaList<object> parentItemItems ) )
                {
                    parentItem.ItemsSource = new AvaloniaList<object>
                    {
                        CreateComponentItem( component, index ),
                    };
                    return;
                }

                TreeViewItem existingItem = FindExistingItem( parentItem, component );

                if ( existingItem != null )
                {
                    existingItem.Tag = component;
                    return;
                }

                TreeViewItem componentItem = CreateComponentItem( component, index );
                parentItemItems.Add( componentItem );
            }
            catch ( Exception ex )
            {
                Logger.LogException( ex, customMessage: "Unexpected exception while creating tree view item" );
            }
        }

        [NotNull]
        private TreeViewItem CreateRootTreeViewItem()
        {
            var checkBox = new CheckBox
            {
                Name = "IsSelected",
            };

            // Set up the event handler for the checkbox
            bool manualSet = false;
            checkBox.Checked += ( sender, e ) =>
            {
                if ( manualSet )
                    return;

                bool allChecked = true;
                var finishedComponents = new HashSet<Component>();
                foreach ( Component component in MainConfig.AllComponents )
                {
                    component.IsSelected = true;
                    ComponentCheckboxChecked( component, finishedComponents, suppressErrors: true );
                }

                foreach ( Component component in MainConfig.AllComponents )
                {
                    if ( component.IsSelected )
                        continue;

                    allChecked = false;
                    break;
                }

                if ( allChecked )
                    return;

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
                    ComponentCheckboxUnchecked( component, finishedComponents, suppressErrors: true );
                }
            };

            var header = new DockPanel();
            header.Children.Add( checkBox );
            header.Children.Add(
                new TextBlock
                {
                    Text = "Available Mods",
                }
            );
            
            var binding = new Binding( path: "IsSelected" );
            if ( ToggleButton.IsCheckedProperty != null )
            {
                _ = checkBox.Bind( ToggleButton.IsCheckedProperty, binding );
            }

            var rootItem = new TreeViewItem
            {
                IsExpanded = true,
                Header = header,
            };
            return rootItem;
        }

        private async Task ProcessComponentsAsync( [NotNull][ItemNotNull] IReadOnlyList<Component> componentsList )
        {
            try
            {
                if ( componentsList.IsNullOrEmptyCollection() )
                    return;

                try
                {
                    ( bool isCorrectOrder, List<Component> reorderedList )
                        = Component.ConfirmComponentsInstallOrder( MainConfig.AllComponents );
                    if ( !isCorrectOrder )
                    {
                        await Logger.LogVerboseAsync( "Reordered list to match dependency structure." );
                        MainConfigInstance.allComponents = reorderedList;
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
                for ( int index = 0; index < componentsList.Count; index++ )
                {
                    Component component = componentsList[index];
                    CreateTreeViewItem( component, rootItem, index );
                }

                // Set the root item as the single item of the tree view
                // Create a collection to hold the root item
                var rootItemsCollection = new AvaloniaList<TreeViewItem>
                {
                    rootItem,
                };

                // Set the root item collection as the items source of the tree view
                LeftTreeView.ItemsSource = rootItemsCollection;

                // Expand the tree. Too lazy to figure out the proper way.
                IEnumerator treeEnumerator = LeftTreeView.Items.GetEnumerator();
                _ = treeEnumerator.MoveNext();
                LeftTreeView.ExpandSubTree( (TreeViewItem)treeEnumerator.Current );

                if ( componentsList.Count > 0 || TabControl is null )
                    return;

                SetTabInternal(TabControl, InitialTab);
            }
            catch ( Exception ex )
            {
                await Logger.LogExceptionAsync( ex );
            }
        }

        [UsedImplicitly]
        private async void AddNewInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var addButton = (Button)sender;
                var thisInstruction = addButton.Tag as Instruction;
                var thisComponent = addButton.Tag as Component;

                if ( thisInstruction is null && thisComponent is null )
                    throw new NullReferenceException( "Cannot find instruction instance from button." );

                int index;
                if ( !( thisComponent is null ) )
                {
                    thisInstruction = new Instruction();
                    index = thisComponent.Instructions.Count;
                    thisComponent.CreateInstruction( index );
                }
                else
                {
                    index = CurrentComponent.Instructions.IndexOf( thisInstruction );
                    CurrentComponent.CreateInstruction( index );
                }

                await Logger.LogVerboseAsync(
                    $"Component '{CurrentComponent.Name}': Instruction '{thisInstruction.Action}' created at index #{index}"
                );

                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void DeleteInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = CurrentComponent.Instructions.IndexOf( thisInstruction );

                CurrentComponent.DeleteInstruction( index );
                await Logger.LogVerboseAsync(
                    $"Component '{CurrentComponent.Name}': instruction '{thisInstruction?.Action}' deleted at index #{index}"
                );

                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void MoveInstructionUp_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = CurrentComponent.Instructions.IndexOf( thisInstruction );

                if ( thisInstruction is null )
                {
                    await Logger.LogExceptionAsync( new InvalidOperationException("The sender does not correspond to a instruction.") );
                    return;
                }

                CurrentComponent.MoveInstructionToIndex( thisInstruction, index - 1 );
                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void MoveInstructionDown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisInstruction = (Instruction)( (Button)sender ).Tag;
                int index = CurrentComponent.Instructions.IndexOf( thisInstruction );

                CurrentComponent.MoveInstructionToIndex( thisInstruction, index + 1 );
                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        public class RelayCommand : ICommand
        {
            [CanBeNull] private readonly Func<object, bool> _canExecute;
            [NotNull] private readonly Action<object> _execute;

            public RelayCommand( [NotNull] Action<object> execute, [CanBeNull] Func<object, bool> canExecute = null )
            {
                _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
                _canExecute = canExecute;
            }

            [UsedImplicitly][CanBeNull] public event EventHandler CanExecuteChanged;

            public bool CanExecute( [CanBeNull] object parameter ) => _canExecute?.Invoke( parameter ) == true;
            public void Execute( [CanBeNull] object parameter ) => _execute( parameter );
        }
        
        private ICommand ItemClickCommand => new RelayCommand(
            parameter =>
            {
                if ( parameter is Component component )
                {
                    LoadComponentDetails( component );
                }
            }
        );

        [UsedImplicitly]
        private void OpenOutputWindow_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            if ( _outputWindow?.IsVisible == true )
            {
                _outputWindow.Close();
            }

            _outputWindow = new OutputWindow();
            _outputWindow.Show();
        }

        private bool _initialize = true;

        [UsedImplicitly]
        private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_initialize)
                {
                    _initialize = false;
                    return;
                }

                var comboBox = (ComboBox)sender;
                var selectedItem = (ComboBoxItem)comboBox?.SelectedItem;

                string stylePath = selectedItem?.Tag as string;

                if (!(stylePath is null) && stylePath.Equals("default"))
                {
                    Styles.Clear();
                    TraverseControls(this, (Control)sender);
                    return;
                }

                var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);

                // Apply the selected style dynamically
                var newStyle = new StyleInclude(styleUriPath)
                {
                    Source = styleUriPath
                };

                Styles.Clear();
                Styles.Add(newStyle);

                TraverseControls(this, (Control)sender);
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
            }
        }


        // Method to get a List<TabItem> from the TabControl
        [UsedImplicitly][NotNull][ItemNotNull]
        public static List<TabItem> GetTabItems( [NotNull] TabControl tabControl)
        {
            if ( tabControl is null )
                throw new ArgumentNullException( nameof( tabControl ) );
            if ( tabControl.Items.IsNullOrEmptyOrAllNull() )
                throw new ArgumentException( $"tabControl.Items failed IsNullOrEmptyOrAllNull({ nameof(tabControl) })" );

            var tabItems = new List<TabItem>();
            // Access the Items property of the TabControl
            // ReSharper disable once PossibleNullReferenceException
            foreach (object item in tabControl.Items)
            {
                // Check if the item is of type TabItem
                if (item is TabItem tabItem)
                {
                    tabItems.Add(tabItem);
                }
            }

            return tabItems;
        }

        private static void TraverseControls(
            [NotNull] Control control,
            [NotNull] Control styleControlComboBox
        )
        {
            if ( control is null )
                throw new ArgumentNullException( nameof( control ) );

            // fixes a crash that can happen while spamming the combobox style options.
            if ( control.Equals(styleControlComboBox) )
                return;

            // Reload the style of the control
            control.ApplyTemplate();

            var logicalControl = control as ILogical;

            // Traverse the child controls recursively
            logicalControl.LogicalChildren.OfType<Control>()
                .ToList()
                .ForEach(
                    childControl => TraverseControls(
                        childControl ?? throw new NullReferenceException( nameof( childControl ) ),
                        styleControlComboBox
                    )
                );
        }

        [UsedImplicitly]
        private void ToggleMaximizeButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            if ( !( sender is Button maximizeButton ) )
                return;

            if ( WindowState == WindowState.Maximized )
            {
                WindowState = WindowState.Normal;
                maximizeButton.Content = "▢";
            }
            else
            {
                WindowState = WindowState.Maximized;
                maximizeButton.Content = "▣";
            }
        }

        [UsedImplicitly]
        private async void AddNewOption_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var addButton = (Button)sender;
                var thisOption = addButton.Tag as Option;
                var thisComponent = addButton.Tag as Component;

                if ( thisOption is null && thisComponent is null )
                    throw new NullReferenceException( "Cannot find option instance from button." );

                int index;
                if ( thisOption is null )
                {
                    thisOption = new Option();
                    index = CurrentComponent.Options.Count;
                }
                else
                {
                    index = CurrentComponent.Options.IndexOf( thisOption );
                }

                CurrentComponent.CreateOption( index );
                await Logger.LogVerboseAsync(
                    $"Component '{CurrentComponent.Name}': Option '{thisOption.Name}' created at index #{index}"
                );

                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void DeleteOption_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisOption = (Option)( (Button)sender ).Tag;
                int index = CurrentComponent.Options.IndexOf( thisOption );

                CurrentComponent.DeleteOption( index );
                await Logger.LogVerboseAsync(
                    $"Component '{CurrentComponent.Name}': instruction '{thisOption?.Name}' deleted at index #{index}"
                );

                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void MoveOptionUp_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisOption = (Option)( (Button)sender ).Tag;
                int index = CurrentComponent.Options.IndexOf( thisOption );

                CurrentComponent.MoveOptionToIndex( thisOption, index - 1 );
                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }

        [UsedImplicitly]
        private async void MoveOptionDown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( CurrentComponent is null )
                {
                    await InformationDialog.ShowInformationDialog( this, message: "Load a component first" );
                    return;
                }

                var thisOption = (Option)( (Button)sender ).Tag;
                int index = CurrentComponent.Options.IndexOf( thisOption );

                CurrentComponent.MoveOptionToIndex( thisOption, index + 1 );
                LoadComponentDetails( CurrentComponent );
            }
            catch ( Exception exception )
            {
                await Logger.LogExceptionAsync( exception );
            }
        }
    }
}
