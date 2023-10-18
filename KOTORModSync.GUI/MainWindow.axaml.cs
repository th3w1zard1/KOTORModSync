﻿// Copyright 2021-2023 KOTORModSync
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
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.CallbackDialogs;
using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemPathing;
using KOTORModSync.Core.Utility;
using Component = KOTORModSync.Core.Component;

// ReSharper disable AsyncVoidMethod

namespace KOTORModSync
{
	[SuppressMessage(category: "ReSharper", checkId: "UnusedParameter.Local")]
	internal sealed partial class MainWindow : Window
	{
		public static readonly DirectProperty<MainWindow, Component> CurrentComponentProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, Component>(
				nameof( CurrentComponent ),
				o => o?.CurrentComponent,
				(o, v) => o.CurrentComponent = v
			);

		[CanBeNull] private Component _currentComponent;
		private bool _ignoreWindowMoveWhenClickingComboBox;

		private bool _initialize = true;

		private bool _installRunning;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		private Window _outputWindow;

		private bool _progressWindowClosed;
		private string _searchText;

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
					new ConfirmationDialogCallback(this),
					new OptionsDialogCallback(this),
					new InformationDialogCallback(this)
				);

				PropertyChanged += SearchText_PropertyChanged;

				// Fixes an annoying problem on Windows where selecting in the console window causes the app to hang.
				// Selection now is only possible through ctrl + m or right click -> mark, which still causes the same hang but is less accidental at least.
				if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
					ConsoleConfig.DisableQuickEdit();
			}
			catch ( Exception e )
			{
				Logger.LogException(e, customMessage: "A fatal error has occurred loading the main window");
				throw;
			}
		}

		public static List<Component> ComponentsList => MainConfig.AllComponents;

		[CanBeNull]
		public string SearchText
		{
			get => _searchText;
			set
			{
				if ( _searchText == value ) return; // prevent recursion problems

				_searchText = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof( SearchText )));
			}
		}

		private MainConfig MainConfigInstance { get; set; }

		[CanBeNull] public Component CurrentComponent
		{
			get => _currentComponent;
			set => SetAndRaise(CurrentComponentProperty, ref _currentComponent, value);
		}

		private bool _ignoreInternalTabChange { get; set; }

		private ICommand ItemClickCommand => new RelayCommand(
			parameter =>
			{
				if ( parameter is Component component )
				{
					LoadComponentDetails(component);
				}
			}
		);
		public new event EventHandler<PropertyChangedEventArgs> PropertyChanged;

		public void InitializeControls()
		{
			if ( MainGrid.ColumnDefinitions == null || MainGrid.ColumnDefinitions.Count != 3 )
				throw new NullReferenceException("MainGrid incorrectly defined, expected 3 columns.");

			// set title and version
			Title = $"KOTORModSync v{MainConfig.CurrentVersion}";
			TitleTextBlock.Text = Title;

			ColumnDefinition componentListColumn = MainGrid.ColumnDefinitions[0]
				?? throw new NullReferenceException("Column 0 of MainGrid (component list column) not defined.");
			ColumnDefinition configColumn = MainGrid.ColumnDefinitions[2]
				?? throw new NullReferenceException("Column 2 of MainGrid (component list column) not defined.");

			// Column 0
			componentListColumn.Width = new GridLength(250);

			// Column 1
			RawEditTextBox.LostFocus +=
				RawEditTextBox_LostFocus; // Prevents RawEditTextBox from being cleared when clicking elsewhere (?)
			RawEditTextBox.DataContext = new ObservableCollection<string>();

			// Column 2
			configColumn.Width = new GridLength(250);
			MainConfigInstance = new MainConfig();

			MainConfigStackPanel.DataContext = MainConfigInstance;

			_ = Logger.LogVerboseAsync("Setting up window move event handlers...");
			// Attach event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
			FindComboBoxesInWindow(this);
		}

		private void SearchText_PropertyChanged(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] PropertyChangedEventArgs e
		)
		{
			try
			{
				if ( e.PropertyName != nameof( SearchText ) )
					return;

				// Get the root item of the TreeView
				var rootItem = (TreeViewItem)LeftTreeView.ContainerFromIndex(0);

				if ( !(rootItem is null) && !(SearchText is null) )
					FilterControlListItems(rootItem, SearchText);
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		public static void FilterControlListItems(
			[JetBrains.Annotations.NotNull] object item,
			[JetBrains.Annotations.NotNull] string searchText
		)
		{
			if ( searchText == null )
				throw new ArgumentNullException(nameof( searchText ));

			if ( !(item is Control controlItem) )
				return; // no components loaded/created

			if ( controlItem.Tag is Component thisComponent )
				ApplySearchVisibility(controlItem, thisComponent.Name, searchText);

			// Iterate through the child items (TreeViewItem only)
			IEnumerable<ILogical> controlItemArray = controlItem.GetLogicalChildren();
			foreach ( TreeViewItem childItem in controlItemArray.OfType<TreeViewItem>() )
			{
				// Recursively filter the child item (TreeViewItem only)
				FilterControlListItems(childItem, searchText);
			}
		}

		private static void ApplySearchVisibility(
			[JetBrains.Annotations.NotNull] Visual item,
			[JetBrains.Annotations.NotNull] string itemName,
			[JetBrains.Annotations.NotNull] string searchText
		)
		{
			if ( item is null )
				throw new ArgumentNullException(nameof( item ));

			if ( itemName is null )
				throw new ArgumentNullException(nameof( itemName ));

			if ( searchText is null )
				throw new ArgumentNullException(nameof( searchText ));

			// Check if the item matches the search text
			// Show or hide the item based on the match
			item.IsVisible = itemName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		// test the options dialog for use with the 'Options' IDictionary<string, object>.
		public async void TestWindow()
		{
			// Create an instance of OptionsDialogCallback
			var optionsDialogCallback = new OptionsDialogCallback(this);

			// Create a list of options
			var options = new List<string>
			{
				"Option 1", "Option 2", "Option 3",
			};

			// Show the options dialog and get the selected option
			string selectedOption = await optionsDialogCallback.ShowOptionsDialog(options);

			// Use the selected option
			if ( selectedOption != null )
			{
				// Option selected, do something with it
				Console.WriteLine("Selected option: " + selectedOption);
			}
			else
			{
				// No option selected or dialog closed
				Console.WriteLine("No option selected or dialog closed");
			}
		}

		private void FindComboBoxes([CanBeNull] Control control)
		{
			if ( !(control is ILogical visual) )
				throw new ArgumentNullException(nameof( control ));

			if ( control is ComboBox )
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
					FindComboBoxes(childControl);
				}
			}
		}

		// Prevents a combobox click from dragging the window around.
		public void FindComboBoxesInWindow([JetBrains.Annotations.NotNull] Window thisWindow)
		{
			if ( thisWindow is null )
				throw new ArgumentNullException(nameof( thisWindow ));

			FindComboBoxes(thisWindow);
		}

		private void ComboBox_Opened(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			_mouseDownForWindowMoving = false;
			_ignoreWindowMoveWhenClickingComboBox = true;
		}

		private void InputElement_OnPointerMoved(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] PointerEventArgs e
		)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			if ( _ignoreWindowMoveWhenClickingComboBox )
			{
				_ignoreWindowMoveWhenClickingComboBox = false;
				_mouseDownForWindowMoving = false;
				return;
			}

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] PointerEventArgs e
		)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			if ( sender is ComboBox )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] PointerEventArgs e
		) =>
			_mouseDownForWindowMoving = false;

		[UsedImplicitly] private void CloseButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) => Close();

		[UsedImplicitly] private void MinimizeButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) =>
			WindowState = WindowState.Minimized;

		[ItemCanBeNull]
		private async Task<string> SaveFile(
			string saveFileName = null,
			[CanBeNull] IReadOnlyList<FilePickerFileType> defaultExts = null
		)
		{
			try
			{
				IStorageFile file = await StorageProvider.SaveFilePickerAsync(
					new FilePickerSaveOptions
					{
						DefaultExtension = "toml",
						FileTypeChoices = /*defaultExts ??*/ new List<FilePickerFileType>
						{
							FilePickerFileTypes.All,
						},
						ShowOverwritePrompt = true,
						SuggestedFileName = saveFileName ?? "my_toml_instructions.toml",
					}
				);

				string filePath = file?.TryGetLocalPath();
				if ( !(filePath is null) )
				{
					await Logger.LogAsync($"Selected file: {filePath}");
					return filePath;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}

			return null;
		}

		[ItemCanBeNull]
		[CanBeNull]
		private async Task<string[]> ShowFileDialog(
			bool isFolderDialog,
			[CanBeNull] IReadOnlyList<FilePickerFileType> filters = null,
			bool allowMultiple = false,
			IStorageFolder startFolder = null,
			string windowName = null
		)
		{
			try
			{
				if ( !(VisualRoot is Window parent) )
				{
					await Logger.LogErrorAsync(
						$"Could not open {(isFolderDialog ? "folder" : "file")} dialog - parent window not found"
					);
					return default;
				}

				string[] results;
				if ( isFolderDialog )
				{
					// Start async operation to open the dialog.
					IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(
						new FolderPickerOpenOptions
						{
							Title = windowName ?? "Choose the folder",
							AllowMultiple = allowMultiple,
							SuggestedStartLocation = startFolder,
						}
					);
					return result.Select(s => s.TryGetLocalPath()).ToArray();
				}
				else
				{
					// Start async operation to open the dialog.
					IReadOnlyList<IStorageFile> result = await StorageProvider.OpenFilePickerAsync(
						new FilePickerOpenOptions
						{
							Title = windowName ?? "Choose the file(s)",
							AllowMultiple = allowMultiple,
							FileTypeFilter = /*filters ?? */new[] // todo: fix custom filters
							{
								FilePickerFileTypes.All, FilePickerFileTypes.TextPlain,
							},
						}
					);
					string[] files = result.Select(s => s.TryGetLocalPath()).ToArray();
					if ( files.Length > 0 )
						return files; // Retrieve the first selected file path
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}

			return null;
		}

		private async Task<bool> FindDuplicateComponents(
			[JetBrains.Annotations.NotNull][ItemNotNull] List<Component> components
		)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof( components ));

			// Check for duplicate GUID
			bool duplicatesFixed = true;
			bool promptUser = true;
			foreach ( Component component in components )
			{
				Component duplicateComponent = components.Find(c => c.Guid == component.Guid && c != component);

				if ( duplicateComponent is null )
					continue;

				if ( !Guid.TryParse(duplicateComponent.Guid.ToString(), out Guid _) )
				{
					_ = Logger.LogWarningAsync(
						$"Invalid GUID for component '{component.Name}'. Got '{component.Guid}'"
					);

					if ( MainConfig.AttemptFixes )
					{
						_ = Logger.LogVerboseAsync("Fixing the above issue automatically...");
						duplicateComponent.Guid = Guid.NewGuid();
					}
				}

				string message =
					$"Component '{component.Name}' has a duplicate GUID with component '{duplicateComponent.Name}'";
				_ = Logger.LogAsync(message);

				bool? confirm = true;
				if ( promptUser )
				{
					confirm = await ConfirmationDialog.ShowConfirmationDialog(
						this,
						$"{message}.{Environment.NewLine}Assign a random GUID to '{duplicateComponent.Name}'? (default: NO)"
					);
				}

				switch ( confirm )
				{
					case true:
						duplicateComponent.Guid = Guid.NewGuid();
						_ = Logger.LogAsync($"Replaced GUID of component '{duplicateComponent.Name}'");
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
		private async void LoadInstallFile_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				// Extension filters for our instruction file
				var filters = new List<FilePickerFileType>
				{
					new FilePickerFileType("toml"), new FilePickerFileType("tml"),
				};

				// Open the file dialog to select a file
				string[] result = await ShowFileDialog(
					windowName: "Load the TOML instruction file you've downloaded/created",
					isFolderDialog: false,
					filters: filters
				);
				if ( result is null || result.Length <= 0 )
					return;

				string filePath = result[0];
				if ( !PathValidator.IsValidPath(filePath) )
					return;

				if ( filePath is null )
					throw new NullReferenceException(nameof( filePath ));

				var thisFile = new FileInfo(filePath);

				// Verify the file
				const int maxInstructionSize = 524288000; // instruction file larger than 500mb is probably unsupported
				if ( thisFile.Length > maxInstructionSize )
				{
					_ = Logger.LogAsync($"Invalid instruction file selected: '{thisFile.Name}'");
					return;
				}

				if ( MainConfig.AllComponents.Count > 0 )
				{
					bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
						this,
						confirmText:
						"You already have a config loaded. Do you want to load this instruction file anyway?"
					);
					if ( confirm != true )
						return;
				}

				// Load components dynamically
				MainConfigInstance.allComponents = Component.ReadComponentsFromFile(filePath);
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		public async void LoadMarkdown_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				// Open the file dialog to select a file
				string[] result = await ShowFileDialog(isFolderDialog: false, windowName: "Load your markdown file.");
				if ( result is null || result.Length <= 0 )
					return;

				string filePath = result[0];
				if ( string.IsNullOrEmpty(filePath) )
					return; // user cancelled

				using ( var reader = new StreamReader(filePath) )
				{
					string fileContents = await reader.ReadToEndAsync();
					if ( MainConfig.AllComponents.Count > 0
						&& await ConfirmationDialog.ShowConfirmationDialog(
							this,
							confirmText: "You already have a config loaded. Do you want to load the markdown anyway?"
						)
						!= true )
					{
						return;
					}

					List<Component> parsedMods = ModParser.ParseMods(string.Join(Environment.NewLine, fileContents))
						?? throw new NullReferenceException(
							"ModParser.ParseMods( string.Join( Environment.NewLine, fileContents ) )"
						);

					MainConfigInstance.allComponents = parsedMods;
					await ProcessComponentsAsync(MainConfig.AllComponents);
				}
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void OpenLink_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] TappedEventArgs e
		)
		{
			if ( !(sender is TextBlock textBlock) )
				return;

			try
			{
				string url = textBlock.Text ?? string.Empty;
				if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri _) )
					throw new InvalidOperationException($"Invalid URL: '{url}'");

				if ( RuntimeInformation.IsOSPlatform(OSPlatform.Windows) )
				{
					_ = Process.Start(
						new ProcessStartInfo
						{
							FileName = url, UseShellExecute = true,
						}
					);
				}
				else if ( RuntimeInformation.IsOSPlatform(OSPlatform.OSX) )
				{
					_ = Process.Start(fileName: "open", url);
				}
				else if ( RuntimeInformation.IsOSPlatform(OSPlatform.Linux) )
				{
					_ = Process.Start(fileName: "xdg-open", url);
				}
				else
				{
					Logger.LogError("Unsupported platform, cannot open link.");
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to open URL: {ex.Message}");
			}
		}

		[UsedImplicitly]
		private async void BrowseSourceFiles_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				var button = (Button)sender;
				// Get the item's data context based on the clicked button
				Instruction thisInstruction = (Instruction)button.DataContext
					?? throw new NullReferenceException("Could not find instruction instance");

				// Open the file dialog to select a file

				IStorageFolder startFolder = null;
				if ( !(MainConfig.SourcePath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.SourcePath.FullName);
				string[] filePaths = await ShowFileDialog(
					windowName: "Select the files to perform this instruction on",
					isFolderDialog: false,
					allowMultiple: true,
					startFolder: startFolder
				);
				if ( filePaths is null )
				{
					await Logger.LogVerboseAsync("User did not select any files.");
					return;
				}

				await Logger.LogVerboseAsync($"Selected files: [{string.Join($",{Environment.NewLine}", filePaths)}]");
				var files = filePaths.ToList();
				if ( files.Count == 0 )
				{
					_ = Logger.LogVerboseAsync(
						"No files chosen in BrowseSourceFiles_Click, returning to previous values"
					);
					return;
				}

				if ( files.IsNullOrEmptyOrAllNull() )
				{
					throw new ArgumentOutOfRangeException(
						nameof( files ),
						$"Invalid files found. Please report this issue to the developer: [{string.Join(separator: ",", files)}]"
					);
				}

				// Replace path with prefixed variables.
				for ( int i = 0; i < files.Count; i++ )
				{
					string filePath = files[i];
					files[i] = MainConfig.SourcePath != null
						? Utility.RestoreCustomVariables(filePath)
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
				// ReSharper disable once InvertIf
				if ( button.Tag is TextBox sourceTextBox )
				{
					string convertedItems = new ListToStringConverter().Convert(
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
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void BrowseDestination_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				Button button = (Button)sender ?? throw new InvalidOperationException();
				Instruction thisInstruction = (Instruction)button.DataContext
					?? throw new NullReferenceException("Could not find instruction instance");

				IStorageFolder startFolder = null;
				if ( !(MainConfig.DestinationPath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.DestinationPath.FullName);

				// Open the folder dialog to select a folder
				string[] result = await ShowFileDialog(isFolderDialog: true, startFolder: startFolder);
				if ( result is null || result.Length <= 0 )
					return;

				string folderPath = result[0];
				if ( folderPath is null )
				{
					_ = Logger.LogVerboseAsync(
						"No folder chosen in BrowseDestination_Click."
						+ $" Will continue using '{thisInstruction.Destination}'"
					);
					return;
				}

				if ( MainConfig.SourcePath is null )
				{
					_ = Logger.LogAsync(
						"Directories not set, setting raw folder path without custom variable <<kotorDirectory>>"
					);
					thisInstruction.Destination = folderPath;
					return;
				}

				thisInstruction.Destination = Utility.RestoreCustomVariables(folderPath);

				// refresh the text box
				if ( button.Tag is TextBox destinationTextBox )
					destinationTextBox.Text = thisInstruction.Destination;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void SaveButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
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

				await Logger.LogVerboseAsync($"Selected '{CurrentComponent.Name}'");

				if ( !await ShouldSaveChanges() )
					return;

				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private async void ResolveDuplicateFilesAndFolders(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				bool? answer = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"This button will resolve all case-sensitive duplicate files/folders in your install directory and your mod download directory."
					+ Environment.NewLine
					+ " WARNING: This method may take a while and cannot be stopped until it finishes. Really continue?"
				);
				if ( answer != true )
					return;

				await Logger.LogAsync("Finding duplicate case-insensitive folders/files in the install destination...");
				IEnumerable<FileSystemInfo> duplicates =
					PathHelper.FindCaseInsensitiveDuplicates(MainConfig.DestinationPath.FullName);
				var fileSystemInfos = duplicates.ToList();
				foreach ( FileSystemInfo duplicate in fileSystemInfos )
				{
					await Logger.LogWarningAsync(duplicate?.FullName + " is duplicated on the storage drive.");
				}

				answer = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"Duplicate file/folder search finished."
					+ Environment.NewLine
					+ $" Found {fileSystemInfos.Count} files/folders that have duplicates in your install dir."
					+ Environment.NewLine
					+ " Delete all duplicates except the ones most recently modified?"
				);
				if ( answer != true )
					return;

				IEnumerable<IGrouping<string, FileSystemInfo>> groupedDuplicates =
					fileSystemInfos.GroupBy(fs => fs.Name.ToLowerInvariant());

				foreach ( IGrouping<string, FileSystemInfo> group in groupedDuplicates )
				{
					var orderedDuplicates = group.OrderByDescending(fs => fs.LastWriteTime).ToList();
					if ( orderedDuplicates.Count <= 1 )
						continue;

					for ( int i = 1; i < orderedDuplicates.Count; i++ )
					{
						try
						{
							switch ( orderedDuplicates[i] )
							{
								case FileInfo fileInfo:
									fileInfo.Delete();
									break;
								case DirectoryInfo directoryInfo:
									directoryInfo.Delete(true); // recursive delete
									break;
								default:
									Logger.Log(orderedDuplicates[i].FullName + " does not exist somehow?");
									continue;
							}

							await Logger.LogAsync($"Deleted {orderedDuplicates[i].FullName}");
						}
						catch ( Exception deletionException )
						{
							await Logger.LogExceptionAsync(
								deletionException,
								$"Failed to delete {orderedDuplicates[i].FullName}"
							);
						}
					}
				}
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async Task<(bool success, string informationMessage)> PreinstallValidation()
		{
			try
			{
				if ( MainConfigInstance is null || MainConfig.DestinationPath is null || MainConfig.SourcePath is null )
				{
					return (false, "Please set your directories first");
				}

				bool holopatcherIsExecutable = true;
				bool holopatcherTestExecute = true;
				if ( MainConfig.PatcherOption == MainConfig.AvailablePatchers.HoloPatcher )
				{
					holopatcherTestExecute = false;
					string holopatcherCliPath = Path.Combine(
						Utility.GetExecutingAssemblyDirectory(),
						path2: "Resources",
						Environment.OSVersion.Platform == PlatformID.Win32NT
							? "holopatcher.exe"
							: "holopatcher"
					);

					await Logger.LogVerboseAsync("Ensuring the holopatcher binary has executable permissions...");
					try
					{
						await PlatformAgnosticMethods.MakeExecutableAsync(holopatcherCliPath);
					}
					catch ( Exception e )
					{
						await Logger.LogExceptionAsync(e);
						holopatcherIsExecutable = false;
					}

					(int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(holopatcherCliPath, cmdlineArgs:"--install");
					if ( result.Item1 == 2 ) // should return syntax error code since we passed no arguments
						holopatcherTestExecute = true;
				}
				else if ( !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) )
				{
					return (false,	"TSLPatcher is not supported on non-windows operating systems, please use the HoloPatcher patcher option.");
				}

				if ( MainConfig.AllComponents.IsNullOrEmptyCollection() )
					return (false, "No instructions loaded! Press 'Load Instructions File' or create some instructions first.");

				if ( !MainConfig.AllComponents.Any(component => component.IsSelected) )
					return (false, "Select at least one mod in the left list to be installed first.");

				await Logger.LogAsync("Finding duplicate case-insensitive folders/files in the install destination...");
				IEnumerable<FileSystemInfo> duplicates =
					PathHelper.FindCaseInsensitiveDuplicates(MainConfig.DestinationPath.FullName);
				var fileSystemInfos = duplicates.ToList();
				foreach ( FileSystemInfo duplicate in fileSystemInfos )
				{
					await Logger.LogErrorAsync(
						duplicate?.FullName + " has a duplicate, please resolve before attempting an install."
					);
				}

				await Logger.LogAsync("Checking for duplicate components...");
				bool noDuplicateComponents = await FindDuplicateComponents(MainConfig.AllComponents);

				// Ensure necessary directories are writable.
				await Logger.LogAsync("Ensuring both the mod directory and the install directory are writable...");
				bool isInstallDirectoryWritable = Utility.IsDirectoryWritable(MainConfig.DestinationPath);
				bool isModDirectoryWritable = Utility.IsDirectoryWritable(MainConfig.SourcePath);

				await Logger.LogAsync("Validating the order of operations and install order of all components...");
				(bool isCorrectOrder, List<Component> reorderedList) =
					Component.ConfirmComponentsInstallOrder(MainConfig.AllComponents);
				if ( !isCorrectOrder && MainConfig.AttemptFixes )
				{
					await Logger.LogWarningAsync("Incorrect order detected, but has been automatically reordered.");
					MainConfigInstance.allComponents = reorderedList;
					isCorrectOrder = true;
				}

				await Logger.LogAsync("Validating individual components, this might take a while...");
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

					var validator = new ComponentValidation(component, MainConfig.AllComponents);
					await Logger.LogVerboseAsync($" == Validating '{component.Name}' == ");
					individuallyValidated &= validator.Run();
				}

				await Logger.LogVerboseAsync("Finished validating all components.");

				string informationMessage = string.Empty;
				if ( !isCorrectOrder )
				{
					informationMessage = "Your components are not in the correct order."
						+ " There are specific mods found that need to be installed either before or after another or more mods."
						+ " Please ensure the correct order, or rerun the validator with 'Attempt Fixes' enabled.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !holopatcherIsExecutable )
				{
					informationMessage =
						"The HoloPatcher binary does not seem to be executable, please see the logs in the output window for more information.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !holopatcherTestExecute )
				{
					informationMessage =
						"The holopatcher test execution did not pass, this may mean the binary is corrupted or has unresolved dependency problems.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !isInstallDirectoryWritable )
				{
					informationMessage = "The Install directory is not writable!"
						+ " Please ensure administrative privileges or reinstall KOTOR"
						+ " to a directory with write access.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !isModDirectoryWritable )
				{
					informationMessage = "The Mod directory is not writable!"
						+ " Please ensure administrative privileges or choose a new mod directory.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !noDuplicateComponents )
				{
					informationMessage = "There were several duplicate components found."
						+ " Please ensure all components are unique and none have conflicting GUIDs.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !individuallyValidated )
				{
					informationMessage = "Some components failed to individually validate.";
					await Logger.LogErrorAsync(informationMessage);
				}

				// ReSharper disable once InvertIf
				if ( fileSystemInfos.Any() )
				{
					informationMessage =
						"You have duplicate files/folders in your installation directory in a case-insensitive environment."
						+ "Please resolve these before continuing. Check the output window for the specific files to resolve.";
					await Logger.LogErrorAsync(informationMessage);
				}

				return !informationMessage.Equals(string.Empty)
					? ((bool success, string informationMessage))(false, informationMessage)
					: ((bool success, string informationMessage))(true,
						"No issues found. If you encounter any problems during the installation, please contact the developer.");
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
				return (false, "Unknown error, check the output window for more information.");
			}
		}

		[UsedImplicitly]
		private async void ValidateButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				(bool _, string informationMessage) = await PreinstallValidation();
				await InformationDialog.ShowInformationDialog(this, informationMessage);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void AddComponentButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			// Create a new default component with a new GUID
			try
			{
				var newComponent = new Component
				{
					Guid = Guid.NewGuid(),
					Name = "new mod_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()),
				};

				// Add the new component to the collection
				MainConfigInstance.allComponents.Add(newComponent);

				// Load into the editor
				LoadComponentDetails(newComponent);

				// Refresh the TreeView to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void RefreshComponents_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) =>
			await ProcessComponentsAsync(MainConfig.AllComponents);

		[UsedImplicitly]
		private async void RemoveComponentButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			// Get the selected component from the TreeView
			try
			{
				if ( CurrentComponent is null )
				{
					Logger.Log("No component loaded into editor - nothing to remove.");
					return;
				}

				// todo:
				if ( MainConfig.AllComponents.Any(c => c.Dependencies.Any(g => g == CurrentComponent.Guid)) )
				{
					await InformationDialog.ShowInformationDialog(
						this,
						$"Cannot remove '{CurrentComponent.Name}', there are several components that rely on it. Please address this problem first."
					);
					return;
				}

				// Remove the selected component from the collection
				_ = MainConfigInstance.allComponents.Remove(CurrentComponent);
				SetCurrentComponent(null);

				// Refresh the TreeView to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void SetDirectories_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				IStorageFolder startFolder = null;
				if ( !(MainConfig.DestinationPath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.DestinationPath.FullName);

				// Open the folder dialog to select a folder
				string[] result = await ShowFileDialog(
					windowName: "Select your <<kotorDirectory>> (path to the game exe)",
					isFolderDialog: true,
					startFolder: startFolder
				);
				if ( result?.Length > 0 )
				{
					string chosenFolder = result[0];
					if ( chosenFolder != null )
					{
						var kotorInstallDir = new DirectoryInfo(chosenFolder);
						MainConfigInstance.destinationPath = kotorInstallDir;
					}
				}
				else
				{
					await Logger.LogVerboseAsync("User cancelled selecting <<kotorDirectory>>");
				}

				if ( !(MainConfig.SourcePath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.SourcePath.FullName)
						?? startFolder;

				// Open the folder dialog to select a folder
				result = await ShowFileDialog(
					windowName: "Select your <<modDirectory>> where ALL your mods are downloaded.",
					isFolderDialog: true,
					startFolder: startFolder
				);
				if ( result?.Length > 0 )
				{
					string chosenFolder = result[0];
					if ( chosenFolder != null )
					{
						var modDirectory = new DirectoryInfo(chosenFolder);
						MainConfigInstance.sourcePath = modDirectory;
					}
				}
				else
				{
					await Logger.LogVerboseAsync("User cancelled selecting <<modDirectory>>");
				}
			}
			catch ( ArgumentNullException ) { }
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "Unknown error - please report to a developer");
			}
		}

		[UsedImplicitly]
		private async void InstallModSingle_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
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
					_ = await informationDialog.ShowDialog<bool?>(this);
					return;
				}

				if ( CurrentComponent is null )
				{
					var informationDialog = new InformationDialog
					{
						InfoText = "Please choose a mod to install from the left list first",
					};
					_ = await informationDialog.ShowDialog<bool?>(this);
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
					await Logger.LogAsync($"User cancelled install of '{name}'");
					return;
				}

				var validator = new ComponentValidation(CurrentComponent, MainConfig.AllComponents);
				await Logger.LogVerboseAsync($" == Validating '{name}' == ");
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
						() => CurrentComponent.InstallAsync(MainConfig.AllComponents)
					);
					_installRunning = false;

					if ( exitCode != 0 )
					{
						await InformationDialog.ShowInformationDialog(
							this,
							$"There was a problem installing '{name}':"
							+ Environment.NewLine
							+ Utility.GetEnumDescription(exitCode)
							+ Environment.NewLine
							+ Environment.NewLine
							+ " Check the output window for details."
						);
					}
					else
					{
						await Logger.LogAsync($"Successfully installed '{name}'");
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
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void StartInstall_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
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

				(bool success, string informationMessage) = await PreinstallValidation();
				if ( !success )
				{
					await InformationDialog.ShowInformationDialog(this, informationMessage);
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

				if ( await ConfirmationDialog.ShowConfirmationDialog(this, confirmText: "Really install all mods?")
					!= true )
				{
					return;
				}
				
				var progressWindow = new ProgressWindow
				{
					ProgressBar =
					{
						Value = 0,
					},
					Topmost = true,
				};

				try
				{
					_ = Logger.LogAsync("Start installing all mods...");
					_installRunning = true;

					progressWindow.Closed += ProgressWindowClosed;
					progressWindow.Show();
					_progressWindowClosed = false;

					Component.InstallExitCode exitCode = Component.InstallExitCode.UnknownError;

					var selectedMods = MainConfig.AllComponents.Where(thisComponent => thisComponent.IsSelected)
						.ToList();
					for ( int index = 0; index < selectedMods.Count; index++ )
					{
						if ( _progressWindowClosed )
						{
							_installRunning = false;
							_ = Logger.LogAsync("User cancelled install by closing the progress window.");
							return;
						}

						Component component = selectedMods[index];
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

								double percentComplete = (double)index / selectedMods.Count;
								progressWindow.ProgressBar.Value = percentComplete;
								progressWindow.InstalledRemaining.Text =
									$"{index}/{selectedMods.Count} Total Installed";
								progressWindow.PercentCompleted.Text = $"{Math.Round(percentComplete * 100)}%";
								progressWindow.Topmost = true;

								// Additional fallback options
								await Task.Delay(millisecondsDelay: 100); // Introduce a small delay
								await Dispatcher.UIThread.InvokeAsync(
									() => { }
								); // Invoke an empty action to ensure UI updates are processed
								await Task.Delay(millisecondsDelay: 50); // Introduce another small delay
							}
						);

						// Ensure the UI updates are processed
						await Task.Yield();
						await Task.Delay(millisecondsDelay: 200);

						if ( !component.IsSelected )
						{
							await Logger.LogAsync($"Skipping install of '{component.Name}' (unchecked)");
							continue;
						}

						await Logger.LogAsync($"Start Install of '{component.Name}'...");
						exitCode = await component.InstallAsync(MainConfig.AllComponents);
						await Logger.LogAsync($"Install of '{component.Name}' finished with exit code {exitCode}");

						if ( exitCode != 0 )
						{
							bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
								this,
								$"There was a problem installing '{component.Name}':"
								+ Environment.NewLine
								+ Utility.GetEnumDescription(exitCode)
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

							await Logger.LogAsync("Install cancelled");
							break;
						}

						await Logger.LogAsync($"Finished installed '{component.Name}'");
					}

					if ( exitCode == Component.InstallExitCode.Success )
					{
						await InformationDialog.ShowInformationDialog(
							this,
							message: "Install Completed. Check the output window for information."
						);
						await Logger.LogAsync("Install completed.");
					}

					progressWindow.Close();
					_installRunning = false;
				}
				catch ( Exception )
				{
					progressWindow.Close();
					_installRunning = false;
					await Logger.LogErrorAsync("Terminating install due to unhandled exception:");
					throw;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private void ProgressWindowClosed([CanBeNull] object sender, [CanBeNull] EventArgs e)
		{
			try
			{
				if ( !(sender is ProgressWindow progressWindow) )
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
				Logger.LogException(exception);
			}
		}

		[UsedImplicitly]
		private async void DocsButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				string file = await SaveFile(
					saveFileName: "mod_documentation.txt",
					new List<FilePickerFileType>
					{
						FilePickerFileTypes.TextPlain,
					}
				);
				if ( file is null )
					return; // user cancelled

				string docs = Component.GenerateModDocumentation(MainConfig.AllComponents);
				await SaveDocsToFileAsync(file, docs);
				string message = $"Saved documentation of {MainConfig.AllComponents.Count} mods to '{file}'";
				await InformationDialog.ShowInformationDialog(this, message);
				_ = Logger.LogAsync(message);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "Error generating and saving documentation");
				await InformationDialog.ShowInformationDialog(
					this,
					message: "An unexpected error occurred while generating and saving documentation."
				);
			}
		}

		private static async Task SaveDocsToFileAsync(
			[JetBrains.Annotations.NotNull] string filePath,
			[JetBrains.Annotations.NotNull] string documentation
		)
		{
			if ( filePath is null )
				throw new ArgumentNullException(nameof( filePath ));
			if ( documentation is null )
				throw new ArgumentNullException(nameof( documentation ));

			try
			{
				if ( !string.IsNullOrEmpty(documentation) )
				{
					using ( var writer = new StreamWriter(filePath) )
					{
						await writer.WriteAsync(documentation);
						await writer.FlushAsync();
						// ReSharper disable once MethodHasAsyncOverload
						// not available in net462
						writer.Dispose();
					}
				}
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
			}
		}

		/// <summary>
		///     Event handler for the TabControl's SelectionChanged event.
		///     This method manages tab selection changes in the TabControl and performs various actions based on the user's
		///     interaction.
		///     When the user selects a different tab, the method first checks if an internal tab change is being ignored. If so,
		///     it immediately returns without performing any further actions.
		///     Additionally, this method relies on a component being currently loaded for proper operation. If no component is
		///     loaded, the method will log a verbose
		///     message, indicating that the tab functionality won't work until a component is loaded.
		///     The method identifies the last selected tab and the newly selected tab and logs their headers to provide user
		///     feedback about their selections.
		///     However, it assumes that the TabControl's SelectionChanged event arguments will always have valid items.
		///     If not, it will log a verbose message, indicating that it couldn't resolve the tab item.
		///     **Caution**: The method tries to resolve the names of the tabs based on their headers, and it assumes that this
		///     information will be available.
		///     If any tab lacks a header, it may lead to unexpected behavior or errors.
		///     If there are no components in the MainConfig or the current component is null, the method defaults to the initial
		///     tab and logs a verbose message.
		///     However, the conditions under which a component is considered "null" or whether the MainConfig contains any valid
		///     components are not explicitly detailed in this method.
		///     The method then compares the names of the current and last selected tabs in lowercase to detect if the user clicked
		///     on the same tab.
		///     If so, it logs a message and returns without performing any further actions.
		///     **Warning**: The logic in this method may trigger swapping of tabs based on certain conditions, such as selecting
		///     the "raw edit" tab or changing from the "raw edit" tab to another.
		///     It is important to be aware of these tab-swapping behaviors to avoid unexpected changes in the user interface.
		///     The method determines whether the tab should be swapped based on the selected tab's name.
		///     If the new tab is "raw edit", it calls the LoadIntoRawEditTextBox method to check if the current component should
		///     be loaded into the raw editor.
		///     The specific criteria for loading a component into the raw editor are not detailed within this method.
		///     If the last tab was "raw edit", the method checks if changes should be saved before swapping to the new tab.
		///     The method finally decides whether to prevent the tab change and returns accordingly.
		///     Depending on the conditions mentioned earlier, tab swapping may be cancelled, which might not be immediately
		///     apparent to the user.
		///     Furthermore, this method modifies the visibility of certain UI elements (RawEditTextBox and ApplyEditorButton)
		///     based on the selected tab.
		///     Specifically, it shows or hides these elements when the "raw edit" tab is selected, which could impact user
		///     interactions if not understood properly.
		/// </summary>
		/// <param name="sender">The object that raised the event (expected to be a TabControl).</param>
		/// <param name="e">The event arguments containing information about the selection change.</param>
		[UsedImplicitly]
		private async void TabControl_SelectionChanged(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] SelectionChangedEventArgs e
		)
		{
			if ( _ignoreInternalTabChange )
				return;

			try
			{
				if ( !(sender is TabControl tabControl) )
				{
					await Logger.LogErrorAsync("Sender is not a TabControl control");
					return;
				}

				if ( CurrentComponent is null )
				{
					await Logger.LogVerboseAsync("No component loaded, tabs can't be used until one is loaded first.");
					SetTabInternal(tabControl, InitialTab);
					return;
				}

				// Get the last selected TabItem
				// ReSharper disable once PossibleNullReferenceException
				if ( e.RemovedItems.IsNullOrEmptyOrAllNull() || !(e.RemovedItems[0] is TabItem lastSelectedTabItem) )
				{
					await Logger.LogVerboseAsync("Previous tab item could not be resolved somehow?");
					return;
				}

				await Logger.LogVerboseAsync($"User is attempting to swap from: {lastSelectedTabItem.Header}");

				// Get the new selected TabItem
				// ReSharper disable once PossibleNullReferenceException
				if ( e.AddedItems.IsNullOrEmptyOrAllNull() || !(e.AddedItems[0] is TabItem attemptedTabSelection) )
				{
					await Logger.LogVerboseAsync("Attempted tab item could not be resolved somehow?");
					return;
				}

				await Logger.LogVerboseAsync($"User is attempting to swap to: {attemptedTabSelection.Header}");

				// Don't show content of any tabs (except the hidden one) if there's no content.
				if ( MainConfig.AllComponents.IsNullOrEmptyCollection() || CurrentComponent is null )
				{
					SetTabInternal(tabControl, InitialTab);
					await Logger.LogVerboseAsync("No config loaded, defaulting to initial tab.");
					return;
				}

				string tabName = GetControlNameFromHeader(attemptedTabSelection)?.ToLowerInvariant();
				string lastTabName = GetControlNameFromHeader(lastSelectedTabItem)?.ToLowerInvariant();

				// do nothing if clicking the same tab
				if ( tabName == lastTabName )
				{
					await Logger.LogVerboseAsync($"Selected tab is already the current tab '{tabName}'");
					return;
				}


				bool shouldSwapTabs = true;
				if ( tabName == "raw edit" )
				{
					shouldSwapTabs = await LoadIntoRawEditTextBox(CurrentComponent);
				}
				else if ( lastTabName == "raw edit" )
				{
					shouldSwapTabs = await ShouldSaveChanges();
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
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				// Get the ComboBox
				if ( !(sender is ComboBox comboBox) )
				{
					Logger.Log("Sender is not a ComboBox.");
					return;
				}

				// Get the instruction
				if ( !(comboBox.DataContext is Instruction thisInstruction) )
				{
					Logger.Log("ComboBox's DataContext must be an instruction for this method.");
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
				Logger.LogException(exception);
			}
		}

		[CanBeNull]
		private TabItem GetCurrentTabItem([CanBeNull] TabControl tabControl) =>
			(tabControl ?? TabControl)?.SelectedItem as TabItem;

		[CanBeNull]
		private static string GetControlNameFromHeader([CanBeNull] TabItem tabItem) => tabItem?.Header?.ToString();

		private void SetTabInternal([JetBrains.Annotations.NotNull] TabControl tabControl, TabItem tabItem)
		{
			if ( tabControl is null )
				throw new ArgumentNullException(nameof( tabControl ));

			_ignoreInternalTabChange = true;
			tabControl.SelectedItem = tabItem;
			_ignoreInternalTabChange = false;
		}

		private async void LoadComponentDetails([JetBrains.Annotations.NotNull] Component selectedComponent)
		{
			if ( selectedComponent == null )
				throw new ArgumentNullException(nameof( selectedComponent ));

			bool confirmLoadOverwrite = true;
			if ( GetControlNameFromHeader(GetCurrentTabItem(TabControl))?.ToLowerInvariant() == "raw edit" )
			{
				confirmLoadOverwrite = await LoadIntoRawEditTextBox(selectedComponent);
			}
			else if ( selectedComponent != CurrentComponent )
			{
				confirmLoadOverwrite = await ShouldSaveChanges();
			}

			if ( !confirmLoadOverwrite )
				return;

			// set the currently tracked component to what's being loaded.
			SetCurrentComponent(selectedComponent);

			// default to SummaryTabItem.
			if ( InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue )
			{
				SetTabInternal(TabControl, SummaryTabItem);
			}
		}

		private void SetCurrentComponent([CanBeNull] Component c) => CurrentComponent = c;

		private async Task<bool> LoadIntoRawEditTextBox([JetBrains.Annotations.NotNull] Component selectedComponent)
		{
			if ( selectedComponent is null )
				throw new ArgumentNullException(nameof( selectedComponent ));

			_ = Logger.LogVerboseAsync($"Loading '{selectedComponent.Name}' into the raw editor...");
			if ( CurrentComponentHasChanges() )
			{
				bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"You're attempting to load the component into the raw editor, but"
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
		private void RawEditTextBox_LostFocus(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) =>
			e.Handled = true;

		private bool CurrentComponentHasChanges() =>
			CurrentComponent != null
			&& !string.IsNullOrWhiteSpace(RawEditTextBox.Text)
			&& RawEditTextBox.Text != CurrentComponent.SerializeComponent();

		/// <summary>
		///     Asynchronous method that determines if changes should be saved before performing an action.
		///     This method checks if the current component has any changes and prompts the user for confirmation if necessary.
		///     The method attempts to deserialize the raw config text from the "RawEditTextBox" into a new Component instance.
		///     If the deserialization process fails due to syntax errors, it will display a confirmation dialog to the user
		///     despite the 'noPrompt' boolean,
		///     offering to discard the changes and continue with the last attempted action. If the user chooses to discard,
		///     the method returns true, indicating that the changes should not be saved.
		///     The method then tries to find the corresponding component in the "MainConfig.AllComponents" collection.
		///     If the index of the current component cannot be found or is out of range, the method logs an error,
		///     displays an information dialog to the user, and returns false, indicating that the changes cannot be saved.
		///     If all checks pass successfully, the method updates the properties of the component in the
		///     "MainConfig.AllComponents" collection
		///     with the deserialized new component, sets the current component to the new one, and refreshes the tree view to
		///     reflect the changes.
		///     **Note**: This method involves multiple asynchronous operations and may not complete immediately.
		///     Any unexpected exceptions that occur during the process are caught, logged, and displayed to the user via an
		///     information dialog.
		/// </summary>
		/// <param name="noPrompt">A boolean flag indicating whether the user should be prompted to save changes. Default is false.</param>
		/// <returns>
		///     True if the changes should be saved or if no changes are detected. False if the user chooses not to save or if
		///     an error occurs.
		/// </returns>
		private async Task<bool> ShouldSaveChanges(bool noPrompt = false)
		{
			string output;
			try
			{
				if ( !CurrentComponentHasChanges() )
				{
					await Logger.LogVerboseAsync("No changes detected, ergo nothing to save.");
					return true;
				}

				if ( !noPrompt
					&& await ConfirmationDialog.ShowConfirmationDialog(
						this,
						confirmText: "Are you sure you want to save?"
					)
					!= true )
				{
					return false;
				}

				// Get the selected component from the tree view
				if ( CurrentComponent is null )
				{
					output = "CurrentComponent is null which shouldn't ever happen in this context."
						+ Environment.NewLine
						+ "Please report this issue to a developer, this should never happen.";

					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(this, output);
					return false;
				}

				var newComponent = Component.DeserializeTomlComponent(RawEditTextBox.Text);
				if ( newComponent is null )
				{
					bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
						this,
						"Could not deserialize your raw config text into a Component instance in memory."
						+ " There may be syntax errors, check the output window for details."
						+ Environment.NewLine
						+ Environment.NewLine
						+ "Would you like to discard your changes and continue with your last attempted action?"
					);

					return confirmResult == true;
				}

				// Find the corresponding component in the collection
				int index = MainConfig.AllComponents.IndexOf(CurrentComponent);
				if ( index == -1 )
				{
					string componentName = string.IsNullOrWhiteSpace(newComponent.Name)
						? "."
						: $" '{newComponent.Name}'.";
					output = $"Could not find the index of component{componentName}"
						+ " Ensure you single-clicked on a component on the left before pressing save."
						+ " Please back up your work and try again.";
					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(this, output);

					return false;
				}

				// Update the properties of the component
				MainConfigInstance.allComponents[index] = newComponent;
				SetCurrentComponent(newComponent);

				// Refresh the tree view to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
				await Logger.LogAsync(
					$"Saved '{newComponent.Name}' successfully. Refer to the output window for more information."
				);
				return true;
			}
			catch ( Exception ex )
			{
				output =
					"An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialog(this, output + Environment.NewLine + ex.Message);
				return false;
			}
		}

		private async void MoveComponentListItem([CanBeNull] Component componentToMove, int relativeIndex)
		{
			try
			{
				int index = MainConfig.AllComponents.IndexOf(componentToMove);
				if ( componentToMove is null
					|| (index == 0 && relativeIndex < 0)
					|| index == -1
					|| index + relativeIndex == MainConfig.AllComponents.Count )
				{
					return;
				}

				_ = MainConfig.AllComponents.Remove(componentToMove);
				MainConfigInstance.allComponents.Insert(index + relativeIndex, componentToMove);
				await ProcessComponentsAsync(MainConfig.AllComponents);
				await Logger.LogVerboseAsync(
					$"Moved '{componentToMove.Name}' to index #{MainConfig.AllComponents.IndexOf(componentToMove) + 1}"
				);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private void MoveUpButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) =>
			MoveComponentListItem(CurrentComponent, relativeIndex: -1);

		[UsedImplicitly]
		private void MoveDownButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		) =>
			MoveComponentListItem(CurrentComponent, relativeIndex: 1);

		[UsedImplicitly]
		private async void SaveModFile_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				string filePath = await SaveFile(
					saveFileName: "my_toml_instructions.toml",
					new List<FilePickerFileType>
					{
						new FilePickerFileType("toml"), new FilePickerFileType("tml"),
					}
				);
				if ( filePath is null )
					return;

				TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
				if ( rootItem is null )
					return;

				await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");

				using ( var writer = new StreamWriter(filePath) )
				{
					foreach ( Component c in MainConfig.AllComponents )
					{
						string tomlContents = c.SerializeComponent();
						await writer.WriteLineAsync(tomlContents);
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private void GenerateGuidButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				GuidGeneratedTextBox.Text = Guid.NewGuid().ToString();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}


		private void ComponentCheckboxChecked(
			[JetBrains.Annotations.NotNull] Component component,
			[JetBrains.Annotations.NotNull] HashSet<Component> visitedComponents,
			bool suppressErrors = false
		)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof( component ));
			if ( visitedComponents is null )
				throw new ArgumentNullException(nameof( visitedComponents ));

			try
			{
				// Check if the component has already been visited
				if ( visitedComponents.Contains(component) )
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
				_ = visitedComponents.Add(component);

				Dictionary<string, List<Component>> conflicts = Component.GetConflictingComponents(
					component.Dependencies,
					component.Restrictions,
					MainConfig.AllComponents
				);

				// Handling conflicts based on what's defined for THIS component
				if ( conflicts.TryGetValue(key: "Dependency", out List<Component> dependencyConflicts) )
				{
					// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
					foreach ( Component conflictComponent in dependencyConflicts )
					{
						// ReSharper disable once InvertIf
						if ( conflictComponent?.IsSelected == false )
						{
							conflictComponent.IsSelected = true;
							ComponentCheckboxChecked(conflictComponent, visitedComponents);
						}
					}
				}

				if ( conflicts.TryGetValue(key: "Restriction", out List<Component> restrictionConflicts) )
				{
					// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
					foreach ( Component conflictComponent in restrictionConflicts )
					{
						// ReSharper disable once InvertIf
						if ( conflictComponent?.IsSelected == true )
						{
							conflictComponent.IsSelected = false;
							ComponentCheckboxUnchecked(conflictComponent, visitedComponents);
						}
					}
				}

				// Handling OTHER component's defined restrictions based on the change to THIS component.
				// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
				foreach ( Component c in MainConfig.AllComponents )
				{
					if ( !c.IsSelected || !c.Restrictions.Contains(component.Guid) )
						continue;

					c.IsSelected = false;
					ComponentCheckboxUnchecked(c, visitedComponents);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		private void ComponentCheckboxUnchecked(
			[JetBrains.Annotations.NotNull] Component component,
			[CanBeNull] HashSet<Component> visitedComponents,
			bool suppressErrors = false
		)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof( component ));

			visitedComponents = visitedComponents ?? new HashSet<Component>();
			try
			{
				// Check if the component has already been visited
				if ( visitedComponents.Contains(component) )
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
					DockPanel headerPanel = (DockPanel)rootItem.Header
						?? throw new InvalidCastException(
							"Your TreeView isn't supported: header must be wrapped by top-level DockPanel"
						);
					CheckBox checkBox = headerPanel.Children.OfType<CheckBox>().FirstOrDefault();

					if ( checkBox != null && !suppressErrors )
					{
						checkBox.IsChecked = null;
					}
				}

				// Add the component to the visited set
				_ = visitedComponents.Add(component);

				// Handling OTHER component's defined dependencies based on the change to THIS component.
				foreach ( Component c in MainConfig.AllComponents )
				{
					if ( c.IsSelected && c.Dependencies.Contains(component.Guid) )
					{
						c.IsSelected = false;
						ComponentCheckboxUnchecked(c, visitedComponents);
					}
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		// Set up the event handler for the checkbox
		private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is CheckBox checkBox) )
					return;
				if ( !(checkBox.Tag is Component thisComponent) )
					return;

				if ( checkBox.IsChecked == true )
					ComponentCheckboxChecked(thisComponent, new HashSet<Component>());
				else if ( checkBox.IsChecked == false )
					ComponentCheckboxUnchecked(thisComponent, new HashSet<Component>());
				else
					Logger.LogVerbose($"Could not determine new checkBox checked bool for {thisComponent.Name}");
			}
			catch ( Exception exception )
			{
				Console.WriteLine(exception);
			}
		}

		[JetBrains.Annotations.NotNull]
		private CheckBox CreateComponentCheckbox([JetBrains.Annotations.NotNull] Component component)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof( component ));

			var checkBox = new CheckBox
			{
				Name = "IsSelected",
				IsChecked = true,
				VerticalContentAlignment = VerticalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Tag = component,
				[ToolTip.TipProperty] = "If checked, this mod will be installed." };
			var binding = new Binding("IsSelected")
			{
				Source = component, Mode = BindingMode.TwoWay,
			};

			checkBox.IsCheckedChanged += OnCheckBoxChanged;

			_ = checkBox.Bind(ToggleButton.IsCheckedProperty, binding);

			return checkBox;
		}

		[JetBrains.Annotations.NotNull]
		private Control CreateComponentHeader([JetBrains.Annotations.NotNull] Component component, int index)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof( component ));

			CheckBox checkBox = CreateComponentCheckbox(component);

			var header = new Grid
			{
				ColumnDefinitions =
				{
					new ColumnDefinition(value: 0, GridUnitType.Auto),
					new ColumnDefinition(value: 0, GridUnitType.Auto),
					new ColumnDefinition(value: 1, GridUnitType.Star),
				},
			};

			header.Children.Add(checkBox);
			Grid.SetColumn(checkBox, value: 0);

			var indexTextBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Text = $"{index + 1}: ",
				FontWeight = FontWeight.DemiBold,
				Margin = new Thickness(left: 0, top: 0, right: 5, bottom: 0) };
			header.Children.Add(indexTextBlock);
			Grid.SetColumn(indexTextBlock, value: 1);

			var nameTextBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center, Text = $"{component.Name}", Focusable = false,
			};
			header.Children.Add(nameTextBlock);
			Grid.SetColumn(nameTextBlock, value: 2);

			return header;
		}


		private TreeViewItem CreateComponentItem([JetBrains.Annotations.NotNull] Component component, int index)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof( component ));

			var componentItem = new TreeViewItem
			{
				Header = CreateComponentHeader(component, index),
				Tag = component,
				IsExpanded = true,
				HorizontalAlignment = HorizontalAlignment.Left };

			componentItem.PointerPressed += (sender, e) =>
			{
				try
				{
					ItemClickCommand?.Execute(component);
					// ReSharper disable once PossibleNullReferenceException
					e.Handled = true; // Prevent event bubbling
				}
				catch ( Exception exception )
				{
					Logger.LogException(exception);
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
			if ( !(parentItem?.Items is IEnumerable items) )
				return null;

			foreach ( object item in items )
			{
				if ( !(item is TreeViewItem treeViewItem) )
					continue;

				if ( treeViewItem.Tag is Component treeViewComponent && treeViewComponent.Equals(component) )
					return treeViewItem;
			}

			return null;
		}

		private void CreateTreeViewItem(
			[JetBrains.Annotations.NotNull] Component component,
			ItemsControl parentItem,
			int index
		)
		{
			try
			{
				if ( parentItem is null )
					throw new ArgumentNullException(nameof( parentItem ));

				if ( component is null )
					throw new ArgumentNullException(nameof( component ));

				if ( !(parentItem.ItemsSource is AvaloniaList<object> parentItemItems) )
				{
					parentItem.ItemsSource = new AvaloniaList<object>
					{
						CreateComponentItem(component, index),
					};
					return;
				}

				TreeViewItem existingItem = FindExistingItem(parentItem, component);

				if ( existingItem != null )
				{
					existingItem.Tag = component;
					return;
				}

				TreeViewItem componentItem = CreateComponentItem(component, index);
				parentItemItems.Add(componentItem);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "Unexpected exception while creating tree view item");
			}
		}

		[JetBrains.Annotations.NotNull]
		private TreeViewItem CreateRootTreeViewItem()
		{
			var rootCheckBox = new CheckBox
			{
				Name = "IsSelected",
			};

			var header = new DockPanel();
			header.Children.Add(rootCheckBox);
			header.Children.Add(
				new TextBlock
				{
					Text = "Available Mods",
				}
			);

			var binding = new Binding(path: "IsSelected");
			_ = rootCheckBox.Bind(ToggleButton.IsCheckedProperty, binding);

			var rootItem = new TreeViewItem
			{
				IsExpanded = true, Header = header,
			};

			// Set up the event handler for the checkbox
			bool manualSet = false;
			rootCheckBox.IsCheckedChanged += (sender, e) =>
			{
				if ( manualSet )
					return;
				if ( !(sender is CheckBox localRootCheckBox) )
					return;

				var finishedComponents = new HashSet<Component>();
				switch ( localRootCheckBox.IsChecked )
				{
					case true:
						foreach ( Component component in MainConfig.AllComponents )
						{
							component.IsSelected = true;
							ComponentCheckboxChecked(component, finishedComponents, suppressErrors: true);
						}

						if ( MainConfig.AllComponents.Any(component => component.IsSelected)
							&& MainConfig.AllComponents.Any(component => !component.IsSelected) )
						{
							manualSet = true;
							localRootCheckBox.IsChecked = null;
							manualSet = false;
						}

						break;
					case false:
						foreach ( Component component in MainConfig.AllComponents )
						{
							component.IsSelected = false;
							ComponentCheckboxUnchecked(component, finishedComponents, suppressErrors: true);
						}

						if ( MainConfig.AllComponents.All(component => !component.IsSelected) )
						{
							manualSet = true;
							localRootCheckBox.IsChecked = false;
							manualSet = false;
						}

						break;
				}

				LeftTreeView.ExpandSubTree(rootItem);
			};

			return rootItem;
		}

		private async Task ProcessComponentsAsync(
			[JetBrains.Annotations.NotNull][ItemNotNull] IReadOnlyList<Component> componentsList
		)
		{
			try
			{
				if ( componentsList.IsNullOrEmptyCollection() )
					return;

				try
				{
					(bool isCorrectOrder, List<Component> reorderedList) =
						Component.ConfirmComponentsInstallOrder(MainConfig.AllComponents);
					if ( !isCorrectOrder )
					{
						await Logger.LogVerboseAsync("Reordered list to match dependency structure.");
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
					CreateTreeViewItem(component, rootItem, index);
				}

				// Set the root item as the single item of the tree view
				// Create a collection to hold the root item
				var rootItemsCollection = new AvaloniaList<TreeViewItem>
				{
					rootItem,
				};

				// Set the root item collection as the items source of the tree view
				LeftTreeView.ItemsSource = rootItemsCollection;

				// Expand the tree.
				LeftTreeView.ExpandSubTree(rootItem);

				if ( componentsList.Count > 0 || TabControl is null )
					return;

				SetTabInternal(TabControl, InitialTab);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void AddNewInstruction_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var addButton = (Button)sender;
				var thisInstruction = addButton.Tag as Instruction;
				var thisComponent = addButton.Tag as Component;

				if ( thisInstruction is null && thisComponent is null )
					throw new NullReferenceException("Cannot find instruction instance from button.");

				int index;
				if ( !(thisComponent is null) )
				{
					thisInstruction = new Instruction();
					index = thisComponent.Instructions.Count;
					thisComponent.CreateInstruction(index);
				}
				else
				{
					index = CurrentComponent.Instructions.IndexOf(thisInstruction);
					CurrentComponent.CreateInstruction(index);
				}

				await Logger.LogVerboseAsync(
					$"Component '{CurrentComponent.Name}': Instruction '{thisInstruction.Action}' created at index #{index}"
				);

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void DeleteInstruction_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisInstruction = (Instruction)((Button)sender).Tag;
				int index = CurrentComponent.Instructions.IndexOf(thisInstruction);

				CurrentComponent.DeleteInstruction(index);
				await Logger.LogVerboseAsync(
					$"Component '{CurrentComponent.Name}': instruction '{thisInstruction?.Action}' deleted at index #{index}"
				);

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveInstructionUp_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisInstruction = (Instruction)((Button)sender).Tag;
				int index = CurrentComponent.Instructions.IndexOf(thisInstruction);

				if ( thisInstruction is null )
				{
					await Logger.LogExceptionAsync(
						new InvalidOperationException("The sender does not correspond to a instruction.")
					);
					return;
				}

				CurrentComponent.MoveInstructionToIndex(thisInstruction, index - 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveInstructionDown_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisInstruction = (Instruction)((Button)sender).Tag;
				int index = CurrentComponent.Instructions.IndexOf(thisInstruction);

				if ( thisInstruction is null )
					throw new NullReferenceException(
						$"Could not get instruction instance from button's tag: {((Button)sender).Content}"
					);

				CurrentComponent.MoveInstructionToIndex(thisInstruction, index + 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void OpenOutputWindow_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			if ( _outputWindow?.IsVisible == true )
			{
				_outputWindow.Close();
			}

			_outputWindow = new OutputWindow();
			_outputWindow.Show();
		}

		[UsedImplicitly]
		private async void StyleComboBox_SelectionChanged(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] SelectionChangedEventArgs e
		)
		{
			try
			{
				if ( _initialize )
				{
					_initialize = false;
					return;
				}

				if ( !(sender is ComboBox comboBox) )
					return;

				var selectedItem = (ComboBoxItem)comboBox.SelectedItem;
				if ( !(selectedItem?.Tag is string stylePath) )
				{
					await Logger.LogErrorAsync("stylePath cannot be rendered from tag, returning immediately");
					return;
				}

				// clear existing style before adding a new one.
				Styles.Clear();
				Styles.Add(new FluentTheme());
				Styles.Clear();

				if ( !stylePath.Equals(value: "default", StringComparison.OrdinalIgnoreCase) )
				{
					// Apply the selected style dynamically
					var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);
					Styles.Add(
						new StyleInclude(styleUriPath)
						{
							Source = styleUriPath,
						}
					);
				}

				// manually update each control in the main window.
				// TraverseControls( this, comboBox );
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			if ( !(sender is Button maximizeButton) )
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
		private async void AddNewOption_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var addButton = (Button)sender;
				var thisOption = addButton.Tag as Option;
				var thisComponent = addButton.Tag as Component;

				if ( thisOption is null && thisComponent is null )
					throw new NullReferenceException("Cannot find option instance from button.");

				int index;
				if ( thisOption is null )
				{
					thisOption = new Option();
					index = CurrentComponent.Options.Count;
				}
				else
				{
					index = CurrentComponent.Options.IndexOf(thisOption);
				}

				CurrentComponent.CreateOption(index);
				await Logger.LogVerboseAsync(
					$"Component '{CurrentComponent.Name}': Option '{thisOption.Name}' created at index #{index}"
				);

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void DeleteOption_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				CurrentComponent.DeleteOption(index);
				await Logger.LogVerboseAsync(
					$"Component '{CurrentComponent.Name}': instruction '{thisOption?.Name}' deleted at index #{index}"
				);

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveOptionUp_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				if ( thisOption is null )
					throw new NullReferenceException(
						$"Could not get option instance from button's tag: {((Button)sender).Content}"
					);

				CurrentComponent.MoveOptionToIndex(thisOption, index - 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveOptionDown_Click(
			[JetBrains.Annotations.NotNull] object sender,
			[JetBrains.Annotations.NotNull] RoutedEventArgs e
		)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				if ( thisOption is null )
					throw new NullReferenceException(
						$"Could not get option instance from button's tag: {((Button)sender).Content}"
					);

				CurrentComponent.MoveOptionToIndex(thisOption, index + 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void CopyTextToClipboard_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await Clipboard.SetTextAsync((string)((MenuItem)sender).DataContext);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		public class RelayCommand : ICommand
		{
			[CanBeNull] private readonly Func<object, bool> _canExecute;
			[JetBrains.Annotations.NotNull] private readonly Action<object> _execute;

			public RelayCommand(
				[JetBrains.Annotations.NotNull] Action<object> execute,
				[CanBeNull] Func<object, bool> canExecute = null
			)
			{
				_execute = execute ?? throw new ArgumentNullException(nameof( execute ));
				_canExecute = canExecute;
			}

#pragma warning disable CS0067
			[UsedImplicitly][CanBeNull] public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

			public bool CanExecute([CanBeNull] object parameter) => _canExecute?.Invoke(parameter) == true;
			public void Execute([CanBeNull] object parameter) => _execute(parameter);
		}
	}
}
