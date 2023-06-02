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
using System.Threading.Tasks;
using System.Windows.Input;

// ReSharper disable once RedundantUsingDirective
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
        private List<Component> _components;
        private readonly ObservableCollection<Component> _selectedComponents = new ObservableCollection<Component>();
        private ObservableCollection<string> _selectedComponentProperties;
        private string _originalContent;
        private MainConfig _mainConfig;
        private Component _currentComponent;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _components = new List<Component>();
            // Find the leftTreeView control and assign it to the member variable
            LeftTreeView = this.FindControl<TreeView>("LeftTreeView");
            RightTextBox = this.FindControl<TextBox>("RightTextBox");
            RightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            RightTextBox.DataContext = _selectedComponentProperties;
            _selectedComponentProperties = new ObservableCollection<string>();
            _mainConfig = new MainConfig();
        }

        public static IControl Build(object data)
        {
            try
            {
                // Create a dictionary to keep track of child TreeViewItems
                var childItems = new Dictionary<string, TreeViewItem>(10000);
                if (!(data is Component component))
                    throw new InvalidCastException("data variable should always be a Component.");

                // If no dependencies we can return here.
                if (component.Dependencies == null || component.Dependencies.Count == 0)
                    return new TextBlock { Text = component.Name }; // Use a TextBlock for components without dependencies

                // Create a TreeViewItem for the component
                var treeViewItem = new TreeViewItem { Header = component.Name };

                // Check if the component has any dependencies
                foreach (string dependency in component.Dependencies)
                {
                    if (childItems.ContainsKey(dependency))
                        continue;

                    // Create a new child TreeViewItem for each unique dependency
                    var childItem = new TreeViewItem { Header = dependency };
                    childItems.Add(dependency, childItem);
                }

                // Add child TreeViewItems to the parent TreeViewItem
                var items = treeViewItem.Items as IList;
                foreach (TreeViewItem childItem in childItems.Values
                    .Where(childItem => childItem == null
                               ? throw new ArgumentNullException(nameof(childItem))
                               : items != null
                    ))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    _ = items.Add(childItem);
                }

                return treeViewItem;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async Task<string> OpenFile()
        {
            try
            {
                var filters = new List<FileDialogFilter>(10)
                {
                    new FileDialogFilter { Name = "Mod Sync File", Extensions = { "toml", "tml" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                };

                string[] result = await ShowFileDialog(false, filters);
                if (result?.Length > 0)
                    return result[0]; // Retrieve the first selected file path
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        private async Task<List<string>> OpenFiles()
        {
            try
            {
                var filters = new List<FileDialogFilter>(10) { new FileDialogFilter { Name = "All Files", Extensions = { "*" } } };

                string[] filePaths = await ShowFileDialog(false, filters, true);
                Logger.Log($"Selected files: {string.Join(", ", filePaths)}");
                return filePaths.ToList();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        private async Task<string> OpenFolder()
        {
            try
            {
                string[] thisFolder = await ShowFileDialog(true, null);
                return thisFolder[0];
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        [ItemCanBeNull]
        private async Task<string> SaveFile(List<string> defaultExt = null)
        {
            try
            {
                if (defaultExt == null)
                {
                    defaultExt = new List<string>() { "toml", "tml" };
                }

                var dialog = new SaveFileDialog
                {
                    DefaultExtension = defaultExt.FirstOrDefault()
                };
                dialog?.Filters?.Add(new FileDialogFilter { Name = "All Files", Extensions = { "*" } });
                if (defaultExt != null)
                    dialog?.Filters?.Add(new FileDialogFilter() { Name = "Preferred Extensions", Extensions = defaultExt });

                // Show the dialog and wait for a result.
                if (VisualRoot is Window parent)
                {
                    string filePath = await dialog.ShowAsync(parent);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        Logger.Log($"Selected file: {filePath}");
                        return filePath;
                    }
                }
                else
                {
                    Logger.Log("Could not open dialog - parent window not found");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
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
                if (!(VisualRoot is Window parent))
                {
                    Logger.Log($"Could not open {(isFolderDialog ? "folder" : "file")} dialog - parent window not found");
                    return default;
                }

                string[] results = isFolderDialog
                    ? (new[] { await new OpenFolderDialog().ShowAsync(parent) })
                    : await new OpenFileDialog() { AllowMultiple = allowMultiple, Filters = filters }.ShowAsync(parent);

                if (results == null || results.Length == 0)
                {
                    Logger.LogVerbose("User did not make a selection");
                    return default;
                }

                Logger.Log($"Selected {(isFolderDialog ? "folder" : "file")}: {string.Join(", ", results)}");
                return results;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        private async void LoadInstallFile_Click(object sender, RoutedEventArgs e)
        {
            // Open the file dialog to select a file
            try
            {
                string filePath = await OpenFile();
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Verify the file type
                string fileExtension = Path.GetExtension(filePath);
                if (!new List<string> { ".toml", ".tml", ".txt" }.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    Logger.Log($"Invalid extension for file {filePath}");
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Load components dynamically
                        _components = FileHelper.ReadComponentsFromFile(filePath);
                        if (!(_components?.Count > 0))
                            return;

                        // Clear existing items in the tree view
                        LeftTreeView.Items = new AvaloniaList<object>();

                        // Create the root item for the tree view
                        var rootItem = new TreeViewItem { Header = "Components" };

                        // Iterate over the components and create tree view items
                        _components.ForEach(component => CreateTreeViewItem(component, rootItem));

                        // Create a collection to hold the root item
                        var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

                        // Set the root item collection as the items source of the tree view
                        LeftTreeView.Items = rootItemsCollection;
                    });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void BrowseSourceFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender;
                // Get the item's data context based on the clicked button
                var thisInstruction = (Instruction)button.DataContext;

                if (thisInstruction == null)
                {
                    Logger.Log("Could not find instruction instance during BrowseSourceFiles_Click");
                    return;
                }

                // Get the TextBox associated with the current item
                var textBox = (TextBox)button.Tag;

                // Open the file dialog to select a file
                List<string> files = await OpenFiles();
                if (files == null)
                {
                    Logger.Log("No files chosen in BrowseSourceFiles_Click, returning to previous values");
                    return;
                }

                if (files.Any(string.IsNullOrEmpty))
                {
                    Logger.LogException(new ArgumentOutOfRangeException(
                                            nameof(files),
                                            $"Invalid files found, please report this to the developer: '{files}'"));
                }

                // Replace path with prefixed variables.
                for (int i = 0; i < files.Count; i++)
                {
                    string filePath = files[i];
                    files[i] = MainConfig.SourcePath != null ? Utility.RestoreCustomVariables(filePath) : filePath;
                }

                if (MainConfig.SourcePath == null)
                    Logger.Log("Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set prior.");
                thisInstruction.Source = files;
            }
            catch (ArgumentNullException ex)
            { Logger.LogVerbose(ex.Message); }
            catch (Exception ex)
            { Logger.LogException(ex); }
        }

        private async void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender ?? throw new InvalidOperationException();
                Instruction thisInstruction = (Instruction)button.DataContext ?? throw new InvalidDataException("Could not find instruction instance during BrowseSourceFiles_Click");

                // Get the TextBox associated with the current item
                var textBox = (TextBox)button.Tag;

                // Open the file dialog to select a file
                string filePath = await OpenFolder() ?? throw new ArgumentNullException($"No file chosen in BrowseDestination_Click. Will continue using {thisInstruction.Destination}");

                if (MainConfig.SourcePath == null)
                {
                    Logger.Log(
                        "Directories not set, setting raw folder path without custom variable <<kotorDirectory>>"
                    );
                    thisInstruction.Destination = filePath;
                    return;
                }

                thisInstruction.Destination = Utility.RestoreCustomVariables(filePath);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentComponent.Guid = Guid.NewGuid();
                LoadComponentDetails(_currentComponent);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentComponent is null)
                {
                    await InformationDialog.ShowInformationDialog(this, "You must select a component from the list, or create one, before saving.");
                    return;
                }

                Logger.LogVerbose($"Selected {_currentComponent.Name}");

                if (!CheckForChanges())
                    return;

                bool confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, "Are you sure you want to save?");
                if (!confirmationResult)
                    return;

                string message = SaveChanges() ? "Saved successfully. Check the output window for more information." : "There were some problems with your syntax, please check the output window.";
                await InformationDialog.ShowInformationDialog(this, message);
                RefreshTreeView();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainConfig != null && MainConfig.DestinationPath != null)
                    return;

                await InformationDialog.ShowInformationDialog(this, "Please set your directories first");
                return;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void AddComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new default component with a new GUID
            try
            {
                Component newComponent = FileHelper.DeserializeTomlComponent(Component.DefaultComponent + Instruction.DefaultInstructions);
                newComponent.Guid = Guid.NewGuid();
                newComponent.Name = "new mod_" + Path.GetRandomFileName();
                // Add the new component to the collection
                _components.Add(newComponent);
                _currentComponent = newComponent;

                // Load into the editor
                LoadComponentDetails(newComponent);
                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void RefreshComponents_Click(object sender, RoutedEventArgs e) => RefreshTreeView();

        private void RemoveComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected component from the TreeView
            try
            {
                if (!(LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem) ||
                    !(selectedTreeViewItem.Tag is Component selectedComponent))
                {
                    return;
                }

                // Remove the selected component from the collection
                _ = _components.Remove(selectedComponent);
                _currentComponent = null;

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void SetDirectories_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await InformationDialog.ShowInformationDialog(this, "Please select your mod directory (where the archives live).");
                string chosenFolder = await OpenFolder();
                DirectoryInfo modDirectory = new DirectoryInfo(chosenFolder);
                await InformationDialog.ShowInformationDialog(this, "Please select your KOTOR(2) directory. (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")");
                chosenFolder = await OpenFolder();
                DirectoryInfo kotorInstallDir = new DirectoryInfo(chosenFolder);
                _mainConfig.UpdateConfig(modDirectory, kotorInstallDir);
            }
            catch (ArgumentNullException)
            {
                Logger.Log("User cancelled selecting folder");
                return;
            }
        }

        private async void InstallModSingle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainConfig == null || MainConfig.DestinationPath == null)
                {
                    var informationDialog = new InformationDialog
                    { InfoText = "Please set your directories first" };
                    _ = await informationDialog.ShowDialog<bool?>(this);
                    return;
                }

                Component thisComponent;
                if (LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
                {
                    thisComponent = (Component)selectedTreeViewItem.Tag;
                }
                else
                {
                    var informationDialog = new InformationDialog
                    { InfoText = "Please choose a mod to install from the left list first" };
                    _ = await informationDialog.ShowDialog<bool?>(this);
                    return;
                }

                if (thisComponent.Directions != null)
                {
                    bool confirm = await ConfirmationDialog.ShowConfirmationDialog(this, thisComponent.Directions + "\r\n\r\n Press Yes to execute these directions now.");
                    if (!confirm)
                    {
                        Logger.Log($"User cancelled install of {thisComponent.Name}");
                        return;
                    }
                }

                var confirmationDialogCallback = new ConfirmationDialogCallback(this);
                (bool success, Dictionary<FileInfo, SHA1> originalChecksums) = await Task.Run(() => thisComponent.ExecuteInstructions(confirmationDialogCallback, _components));
                if (!success)
                    await InformationDialog.ShowInformationDialog(this, $"There was a problem installing {thisComponent.Name}, please check the output window");
                else
                    Logger.Log($"Successfully installed {thisComponent.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void StartInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainConfig == null || MainConfig.DestinationPath == null)
                {
                    await InformationDialog.ShowInformationDialog(this, "Please set your directories first");
                    return;
                }

                if (_components.Count == 0)
                {
                    await InformationDialog.ShowInformationDialog(this, "No instructions loaded! Press 'Load Instructions File' or create some instructions first.");
                    return;
                }

                if (!await ConfirmationDialog.ShowConfirmationDialog(this, "Really install all mods?"))
                {
                    return;
                }

                Logger.Log("Start installing all mods...");
                var progressWindow = new ProgressWindow();
                progressWindow.Closed += ProgressWindowClosed;
                progressWindow.progressBar.Value = 0;
                progressWindow.Show();

                foreach (Component component in _components)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        progressWindow.progressTextBlock.Text = $"Installing {component.Name}...\nDirections: {component.Directions}";
                        progressWindow.progressBar.Value = 0;

                        // Additional fallback options
                        await Task.Delay(100); // Introduce a small delay
                        await Dispatcher.UIThread.InvokeAsync(() => { }); // Invoke an empty action to ensure UI updates are processed
                        await Task.Delay(50); // Introduce another small delay
                    });

                    // Ensure the UI updates are processed
                    await Task.Yield();
                    await Task.Delay(200);

                    // Further code execution

                    // Call the ExecuteInstructions method asynchronously using Task.Run
                    Logger.Log($"Call ExecuteInstructions for {component.Name}...");
                    (bool success, Dictionary<FileInfo, SHA1> originalChecksums) = await component.ExecuteInstructions(new ConfirmationDialogCallback(this), _components);
                    if (!success)
                    {
                        if (
                            !await ConfirmationDialog.ShowConfirmationDialog(
                                this,
                                $"There was a problem installing {component.Name}, please check the output window.\n\nContinue with the next mod anyway?"
                            )
                        )
                        {
                            break;
                        }
                    }
                    else
                    {
                        Logger.Log($"Successfully installed {component.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void ProgressWindowClosed(object sender, EventArgs e)
        {
            if (!(sender is ProgressWindow progressWindow))
                return;
            progressWindow.progressBar.Value = 0;
            progressWindow.Closed -= ProgressWindowClosed;
            progressWindow.Dispose();
        }

        private async void DocsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string file = await SaveFile(new List<string>(65535) { "txt" });
                if (file == null)
                    return;

                string docs = Serializer.GenerateModDocumentation(_components);
                await SaveDocsToFile(file, docs);
                string message = $"Saved documentation of {_components.Count} mods to '{file}'";
                await InformationDialog.ShowInformationDialog(this, message);
                Logger.Log(message);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error generating and saving documentation: {ex.Message}");
                await InformationDialog.ShowInformationDialog(this, "An error occurred while generating and saving documentation.");
            }
        }

        private static async Task SaveDocsToFile(string filePath, string documentation)
        {
            try
            {
                await new StreamWriter(filePath).WriteAsync(documentation);
            }
            catch (Exception e)
            { Logger.LogException(e); }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!((sender as TabControl)?.SelectedItem is TabItem selectedItem))
                return;
            if (selectedItem.Header == null)
                return;

            // Show/hide the appropriate content based on the selected tab
            if (selectedItem.Header.ToString() == "Raw Edit")
                RightTextBox.IsVisible = true;
            else if (selectedItem?.Header.ToString() == "GUI Edit")
                RightTextBox.IsVisible = false;
        }

        private async void LoadComponentDetails(
            Component selectedComponent,
            bool confirmation = true
        )
        {
            try
            {
                if (selectedComponent == null || RightTextBox == null)
                    return;

                Logger.LogVerbose($"Loading {selectedComponent.Name}...");
                // todo: figure out what we're doing with _originalComponent
                _originalContent = selectedComponent.SerializeComponent();
                if (_originalContent != RightTextBox.Text && !string.IsNullOrEmpty(RightTextBox.Text) && selectedComponent != _currentComponent)
                {
                    // double check with user before overwrite
                    if (confirmation && !await ConfirmationDialog.ShowConfirmationDialog(
                            this,
                            "You're attempting to load the component, but there may be unsaved changes still in the editor. Really continue?"
                        )
                        )
                    {
                        return;
                    }
                }

                // populate raw editor
                RightTextBox.Text = _originalContent;
                // this tracks the currently selected component.
                _currentComponent = selectedComponent;
                // interestingly the variable 'ComponentsItemsControl' is already defined in this scope, but accessing it directly doesn't function the same.
                ItemsControl componentsItemsControl = this.FindControl<ItemsControl>("ComponentsItemsControl");
                // bind the selected component to the gui editor
                componentsItemsControl.Items = new ObservableCollection<Component>
                {
                    selectedComponent
                };
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void RightListBox_LostFocus(object sender, RoutedEventArgs e) => e.Handled = true;

        private bool CheckForChanges()
        {
            string currentContent = RightTextBox.Text;
            return !string.Equals(currentContent, _originalContent);
        }

        private async void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;

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
            await Task.Delay(100);
            if (!textBox.IsFocused)
            {
                LoadComponentDetails(_currentComponent);
            }
        }

        private bool SaveChanges()
        {
            try
            {
                // Get the selected component from the tree view
                if (LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
                {
                    Component newComponent = FileHelper.DeserializeTomlComponent(RightTextBox.Text);

                    // Find the corresponding component in the collection
                    int index = _components.IndexOf(selectedComponent);
                    if (index < 0)
                    {
                        throw new IndexOutOfRangeException(
                            "Could not find index of component."
                            + " Ensure you single clicked on a component on the left before pressing save."
                            + " Please back up your work and try again."
                        );
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
            catch (IndexOutOfRangeException ex)
            {
                Logger.LogException(ex);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return false;
        }

        private void MoveTreeViewItem(ItemsControl parentItemsControl, TreeViewItem selectedTreeViewItem, int newIndex)
        {
            try
            {
                List<Component> componentsList = _components; // Use the original components list
                int currentIndex = componentsList.IndexOf((Component)selectedTreeViewItem.Tag);

                if (currentIndex == -1 || newIndex < 0 || newIndex >= componentsList.Count)
                    return;

                componentsList.RemoveAt(currentIndex);
                componentsList.Insert(newIndex, (Component)selectedTreeViewItem.Tag);
                LeftTreeView.SelectedItem = selectedTreeViewItem;

                // Update the visual tree directly to reflect the changes
                var parentItemsCollection = (AvaloniaList<object>)parentItemsControl.Items;
                parentItemsCollection.Move(currentIndex, newIndex);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Logger.LogException(ex);
                Logger.Log("Will fix above error in a future version - sorry.");
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem) ||
                    !(selectedTreeViewItem.Parent is ItemsControl parentItemsControl))
                {
                    return;
                }

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf(selectedTreeViewItem);
                MoveTreeViewItem(parentItemsControl, selectedTreeViewItem, currentIndex - 1);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(LeftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem) ||
                    !(selectedTreeViewItem.Parent is ItemsControl parentItemsControl))
                {
                    return;
                }

                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf(selectedTreeViewItem);
                MoveTreeViewItem(parentItemsControl, selectedTreeViewItem, currentIndex + 1);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void SaveModFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = await SaveFile();
                if (filePath == null)
                {
                    return;
                }

                TreeViewItem rootItem = LeftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
                if (rootItem != null)
                {
                    WriteTreeViewItemsToFile(new List<TreeViewItem> { rootItem }, filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
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
                foreach (Component component in _components)
                {
                    CreateTreeViewItem(component, rootItem);
                }

                // Set the root item as the single item of the tree view
                LeftTreeView.Items = new AvaloniaList<object> { rootItem };

                // Expand the root item to automatically expand the tree view
                rootItem.IsExpanded = true;

                WriteTreeViewItemsToFile(new List<TreeViewItem> { rootItem }, null);
                //currentComponent = null;
            }
            catch (ArgumentException ex)
            {
                Logger.LogException(ex);
                Logger.Log("Ensure your config file does not have any duplicate mods defined.");
            }
        }

        private void CreateTreeViewItem(Component component, TreeViewItem parentItem)
        {
            try
            {
                // Check if the component item is already added to the parent item
                string componentName = component.Name;
                TreeViewItem existingItem = parentItem.Items.OfType<TreeViewItem>()
                    .FirstOrDefault(
                        item => item.Header.ToString()
                            .Equals(componentName, StringComparison.Ordinal)
                    );

                if (existingItem != null)
                {
                    // Update the existing item's tag with the component
                    existingItem.Tag = component;
                    return;
                }

                // Check for duplicate GUID
                Component duplicateComponent = _components
                    .Find(c =>
                          {
                              string cName = c.Name;
                              return c.Guid == component.Guid && c != component && cName == componentName;
                          });

                if (duplicateComponent != null)
                {
                    string message = $"Component '{component.Name}' has duplicate GUID with component '{duplicateComponent.Name}'";
                    Logger.Log(message);
                    bool confirm = ConfirmationDialog.ShowConfirmationDialog(this, message + $".\r\nAssign random GUID to '{duplicateComponent.Name}'? (default: NO)").GetAwaiter().GetResult();
                    if (confirm)
                    {
                        duplicateComponent.Guid = Guid.NewGuid();
                        Logger.Log($"Replaced guid of component {duplicateComponent.Name}");
                    }
                }

                // Create a new tree view item for the component
                var componentItem = new TreeViewItem
                {
                    Header = component.Name,
                    Tag = component // this allows us to access the item later
                };

                // Assign the ItemClickCommand to the componentItem
                componentItem.DoubleTapped += (sender, e) =>
                {
                    if (_selectedComponents.Contains(component))
                        _ = _selectedComponents.Remove(component);

                    ItemClickCommand.Execute(component);
                };

                // Add the component item to the parent item
                ((AvaloniaList<object>)parentItem.Items).Add(componentItem);

                // Check if the component has dependencies
                if (component?.Dependencies == null || component.Dependencies.Count == 0)
                    return;

                // Iterate over the dependencies and create tree view items
                foreach (string dependencyGuid in component.Dependencies)
                {
                    try
                    {
                        // Find the dependency in the components list
                        Component dependency = _components.Find(c => c.Guid == new Guid(dependencyGuid));

                        if (dependency == null)
                            continue;
                        // Create the dependency tree view item
                        CreateTreeViewItem(dependency, componentItem);
                    }
                    catch (FormatException ex)
                    {
                        // Usually catches invalid guid from the user
                        Logger.LogException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(new Exception($"Error creating tree view item: {ex.Message}"));
            }
        }

        private void WriteTreeViewItemsToFile(List<TreeViewItem> items, string filePath)
        {
            string randomFileName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName());
            filePath = filePath ?? $"modconfig_{randomFileName}.toml";
            Logger.Log($"Creating backup modconfig at {filePath}");

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (TreeViewItem item in items)
                {
                    WriteTreeViewItemToFile(item, writer, maxDepth: 1);
                }
            }
        }

        private static void WriteTreeViewItemToFile(TreeViewItem item, TextWriter writer, int depth = 0, int maxDepth = int.MaxValue)
        {
            if (item.Tag is Component component)
            {
                string tomlContents = component.SerializeComponent();
                writer.WriteLine(tomlContents);
            }

            if (depth >= maxDepth || item.Items == null)
                return;

            foreach (TreeViewItem childItem in item.Items.OfType<TreeViewItem>())
            {
                WriteTreeViewItemToFile(childItem, writer, depth + 1, maxDepth);
            }
        }

        private void RightDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle the selection changed event, if needed
        }

        private RelayCommand _itemClickCommand;
        public ICommand ItemClickCommand => _itemClickCommand ?? (_itemClickCommand = new RelayCommand(ItemClick));

        private void ItemClick(object parameter)
        {
            if (parameter is Core.Component component)
            {
                // Handle the item click event here
                if (!_selectedComponents.Contains(component))
                {
                    _selectedComponents.Add(component);
                }

                LoadComponentDetails(component);
            }
        }

        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Func<object, bool> _canExecute;

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                this._execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this._canExecute = canExecute;
            }

#pragma warning disable CS0067 // warning is incorrect - it's used internally.

            public event EventHandler CanExecuteChanged;

#pragma warning restore CS0067

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

            public void Execute(object parameter) => _execute(parameter);
        }
    }

    public class ComboBoxItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string action ? new ComboBoxItem { Content = action } : (object)null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is ComboBoxItem comboBoxItem ? (comboBoxItem.Content?.ToString()) : (object)null;
    }

    public class EmptyCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICollection collection && collection.Count == 0)
                return new List<string>() { string.Empty }; // Create a new collection with a default value

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is IEnumerable<string> list ? string.Join(Environment.NewLine, list) : (object)string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string text))
                return Enumerable.Empty<string>();

            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            return lines.ToList();
        }
    }

    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool isExpanded && targetType == typeof(string) ? isExpanded ? "▼" : "▶" : (object)string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}