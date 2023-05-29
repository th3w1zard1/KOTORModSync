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
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.GUI
{
    public partial class MainWindow : Window
    {
        private List<Component> _components;
        private readonly ObservableCollection<Component> _selectedComponents = new ObservableCollection<Component>();
        private ObservableCollection<string> _selectedComponentProperties;
        private string _originalContent;
        private MainConfig _mainConfig;
        private Component currentComponent;

        public List<string> AvailableActions { get; } = new List<string>()
        {
            "execute",
            "tslpatcher",
            "move",
            "delete"
        };

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
            leftTreeView = this.FindControl<TreeView>("leftTreeView");
            rightTextBox = this.FindControl<TextBox>("rightTextBox");
            rightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            rightTextBox.DataContext = _selectedComponentProperties;
            _selectedComponentProperties = new ObservableCollection<string>();
            _mainConfig = new MainConfig();
        }

        public static IControl Build(object data)
        {
            if (data is Component component)
            {
                // Check if the component has any dependencies
                if (component.Dependencies == null || component.Dependencies.Count == 0)
                {
                    // Use a TextBlock for components without dependencies
                    return new TextBlock { Text = component.Name };
                }
                else
                {
                    // Create a TreeViewItem for the component
                    var treeViewItem = new TreeViewItem { Header = component.Name };

                    // Create a dictionary to keep track of child TreeViewItems
                    var childItems = new Dictionary<string, TreeViewItem>();

                    foreach (string dependency in component.Dependencies)
                    {
                        if (!childItems.ContainsKey(dependency))
                        {
                            // Create a new child TreeViewItem for each unique dependency
                            var childItem = new TreeViewItem { Header = dependency };
                            childItems.Add(dependency, childItem);
                        }
                    }

                    // Add child TreeViewItems to the parent TreeViewItem
                    var items = treeViewItem.Items as IList;
                    foreach (TreeViewItem childItem in childItems.Values)
                    {
                        _ = items.Add(childItem);
                    }

                    return treeViewItem;
                }
            }

            // Return null if the item is not a Component
            return null;
        }

        private async Task<string> OpenFile()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                AllowMultiple = false
            };
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "Mod Sync File", Extensions = { "toml", "tml" } });
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });

            // Show the dialog and wait for a result.
            if (VisualRoot is Window parent)
            {
                string[] strings = await dialog.ShowAsync(parent);
                string[] files = strings;
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    Logger.Log($"Selected file: {filePath}");
                    return filePath;
                }
            }
            else
            {
                Logger.Log("Could not open dialog - parent window not found");
            }

            return null;
        }

        private async Task<List<string>> OpenFiles()
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = true
            };

            var filter = new FileDialogFilter { Name = "Mod Sync File", Extensions = { "toml", "tml" } };
            dialog.Filters.Add(filter);
            dialog.Filters.Add(new FileDialogFilter { Name = "All Files", Extensions = { "*" } });

            // Show the dialog and wait for a result.
            var parent = VisualRoot as Window;
            if (parent != null)
            {
                var filePaths = await dialog.ShowAsync(parent);
                if (filePaths != null && filePaths.Length > 0)
                {
                    Logger.Log($"Selected files: {string.Join(", ", filePaths)}");
                    return filePaths.ToList();
                }
            }
            else
            {
                Logger.Log("Could not open dialog - parent window not found");
            }

            return null;
        }


        private async Task<string> SaveFile(List<string> defaultExt = null)
        {
            if (defaultExt == null)
            {
                defaultExt = new List<string>() { "toml", "tml" };
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                DefaultExtension = defaultExt.FirstOrDefault()
            };
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "Mod Sync File", Extensions = defaultExt });

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

            return null;
        }

        private async void LoadInstallFile_Click(object sender, RoutedEventArgs e)
        {
            // Open the file dialog to select a file
            string filePath = await OpenFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                // Verify the file type
                string fileExtension = System.IO.Path.GetExtension(filePath);
                if (new[] { ".toml", ".tml" }.Any(ext => string.Equals(fileExtension, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Clear existing items in the tree view
                        leftTreeView.Items = new AvaloniaList<object>();

                        // Load components dynamically
                        _components = Serializer.FileHandler.ReadComponentsFromFile(filePath);
                        if (_components != null && _components.Count > 0)
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

                            // Create a collection to hold the root item
                            var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

                            // Set the root item collection as the items source of the tree view
                            leftTreeView.Items = rootItemsCollection;
                        }
                    });
                }
            }
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainConfig == null || MainConfig.DestinationPath == null)
            {
                await InformationDialog.ShowInformationDialog(this, "Please set your directories first");
                return;
            }
            return;
        }

        private void AddComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new default component with a new GUID
            var newComponent = new Component
            {
                Guid = Guid.NewGuid().ToString(),
                Name = "new mod_" + Path.GetRandomFileName()
            };

            // Add the new component to the collection
            _components.Add(newComponent);
            currentComponent = newComponent;

            // Refresh the TreeView to reflect the changes
            RefreshTreeView();

            // Set the example deserialized string for the new component
            rightTextBox.Text = Component.defaultComponent + Instruction.defaultInstructions;
        }

        private void RemoveComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected component from the TreeView
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
            {
                // Remove the selected component from the collection
                _ = _components.Remove(selectedComponent);
                currentComponent = null;

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
        }

        private void RefreshComponents_Click(object sender, RoutedEventArgs e) => RefreshTreeView();

        private async Task<string> OpenFolder()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();

            // Show the dialog and wait for a result.
            if (VisualRoot is Window parent)
            {
                string selectedFolder = await dialog.ShowAsync(parent);
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    Logger.Log($"Selected folder: {selectedFolder}");
                    return selectedFolder;
                }
            }
            else
            {
                Logger.Log("Could not open folder dialog - parent window not found");
            }

            return null;
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
            if (_mainConfig == null || MainConfig.DestinationPath == null)
            {
                var informationDialog = new InformationDialog
                {
                    InfoText = "Please set your directories first"
                };
                _ = await informationDialog.ShowDialog<bool?>(this);
                return;
            }
            Component thisComponent;
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
            {
                thisComponent = (Component)selectedTreeViewItem.Tag;
            }
            else
            {
                var informationDialog = new InformationDialog
                {
                    InfoText = "Please choose a mod to install from the left list first"
                };
                _ = await informationDialog.ShowDialog<bool?>(this);
                return;
            }
            var confirmationDialogCallback = new ConfirmationDialogCallback(this);

            // Call the ExecuteInstructions method asynchronously using Task.Run
            try
            {
                if (thisComponent.Directions != null)
                {
                    _ = InformationDialog.ShowInformationDialog(this, thisComponent.Directions);
                }

                /* Unmerged change from project 'KOTORModSync (net6.0)'
                Before:
                                var (success, originalChecksums) = await Task.Run(() => thisComponent.ExecuteInstructions(confirmationDialogCallback, _components));
                After:
                                (bool success, originalChecksums) = await Task.Run(() => thisComponent.ExecuteInstructions(confirmationDialogCallback, _components));
                */
                (bool success, Dictionary<FileInfo, SHA1> originalChecksums) = await Task.Run(() => thisComponent.ExecuteInstructions(confirmationDialogCallback, _components));
                if (!success)
                {
                    await InformationDialog.ShowInformationDialog(this, $"There was a problem installing {thisComponent.Name}, please check the output window");
                    return;
                }
                else
                {
                    Logger.Log($"Successfully installed {thisComponent.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void StartInstall_Click(object sender, RoutedEventArgs e)
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

            for (int i = 0; i < _components.Count; i++)
            {
                Component component = _components[i];
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
                    if (!await ConfirmationDialog.ShowConfirmationDialog(this, $"There was a problem installing {component.Name}, please check the output window.\n\nContinue with the next mod anyway?"))
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

        private void ProgressWindowClosed(object sender, EventArgs e)
        {
            if (sender is ProgressWindow progressWindow)
            {
                progressWindow.Closed -= ProgressWindowClosed;
                progressWindow.Dispose();
            }
        }

        private async void DocsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string file = await SaveFile(new List<string> { "txt" });
                if (file != null)
                {
                    string docs = Serializer.GenerateModDocumentation(_components);
                    await SaveDocsToFile(file, docs);
                    string message = $"Saved documentation of {_components.Count} mods to '{file}'";
                    await InformationDialog.ShowInformationDialog(this, message);
                    Logger.Log(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error generating and saving documentation: {ex.Message}");
                await InformationDialog.ShowInformationDialog(this, "An error occurred while generating and saving documentation.");
            }
        }

        private async Task SaveDocsToFile(string filePath, string documentation)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                await writer.WriteAsync(documentation);
            }
        }

        private ICommand itemClickCommand;

        public ICommand ItemClickCommand => itemClickCommand ?? (itemClickCommand = new RelayCommand(ItemClick));

        private void ItemClick(object parameter)
        {
            if (parameter is Core.Component component)
            {
                // Handle the item click event here
                if (!_selectedComponents.Contains(component))
                {
                    _selectedComponents.Add(component);
                }
                PopulateRightTextBox(component);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the selected tab item
            var selectedItem = (sender as TabControl)?.SelectedItem as TabItem;

            // Show/hide the appropriate content based on the selected tab
            if (selectedItem?.Header.ToString() == "Raw Edit")
            {
                rightTextBox.IsVisible=true;
            }
            else if (selectedItem?.Header.ToString() == "GUI Edit")
            {
                rightTextBox.IsVisible=false;
            }
        }


        private void PopulateRightTextBox(Core.Component selectedComponent)
        {
            if (selectedComponent != null && rightTextBox != null)
            {
                _originalContent = Serializer.SerializeComponent(selectedComponent);
                rightTextBox.Text = _originalContent;
                currentComponent = selectedComponent;
                var componentsItemsControl = this.FindControl<ItemsControl>("componentsItemsControl");
                var componentCollection = new ObservableCollection<Component>();
                componentsItemsControl.Items = componentCollection;
                componentCollection.Add(currentComponent);
            }
        }

        private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            currentComponent.Guid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
            PopulateRightTextBox(currentComponent);
        }

        private void RightListBox_LostFocus(object sender, RoutedEventArgs e) => e.Handled = true;
        private bool CheckForChanges()
        {
            string currentContent = rightTextBox.Text;
            return !string.Equals(currentContent, _originalContent);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentComponent is null && leftTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                currentComponent = selectedItem.Tag as Component;
            }
            if (currentComponent is null)
            {
                await InformationDialog.ShowInformationDialog(this, "You must select a component from the list, or create one, before saving.");
            }
            else if (CheckForChanges())
            {
                bool confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, "Are you sure you want to save?");
                if (confirmationResult)
                {
                    string message = SaveChanges() ? "Saved successfully. Check the output window for more information." : "There were some problems with your syntax, please check the output window.";
                    await InformationDialog.ShowInformationDialog(this, message);
                    RefreshTreeView();
                }
            }
        }

        public bool SaveChanges()
        {
            // Get the selected component from the tree view
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
            {
                try
                {
                    Component newComponent = Serializer.FileHandler.DeserializeTomlComponent(rightTextBox.Text);
                    if (newComponent != null)
                    {
                        // Find the corresponding component in the collection
                        int index = _components.IndexOf(selectedComponent);
                        if (index >= 0)
                        {
                            // Update the properties of the component
                            _components[index] = newComponent;
                            RefreshTreeView(); // Refresh the tree view to reflect the changes
                            leftTreeView.SelectedItem = newComponent; // Select the updated component in the tree view
                            return true;
                        }
                        Logger.LogException(new Exception("Could not find index of component. Ensure you single clicked on a component on the left before pressing save. Please back up your work and try again."));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
                return false;
            }

            Logger.LogException(new Exception("Original component is null somehow"));
            return false;
        }

        private void MoveTreeViewItem(ItemsControl parentItemsControl, TreeViewItem selectedTreeViewItem, int newIndex)
        {
            try
            {
                List<Component> componentsList = _components; // Use the original components list
                int currentIndex = componentsList.IndexOf((Component)selectedTreeViewItem.Tag);

                if (currentIndex != -1 && newIndex >= 0 && newIndex < componentsList.Count)
                {
                    componentsList.RemoveAt(currentIndex);
                    componentsList.Insert(newIndex, (Component)selectedTreeViewItem.Tag);
                    leftTreeView.SelectedItem = selectedTreeViewItem;

                    // Update the visual tree directly to reflect the changes
                    var parentItemsCollection = (AvaloniaList<object>)parentItemsControl.Items;
                    parentItemsCollection.Move(currentIndex, newIndex);
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Logger.LogException(ex);
                Logger.Log("Will fix this in the next version - sorry.");
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Parent is ItemsControl parentItemsControl)
            {
                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf(selectedTreeViewItem);
                MoveTreeViewItem(parentItemsControl, selectedTreeViewItem, currentIndex - 1);
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Parent is ItemsControl parentItemsControl)
            {
                int currentIndex = parentItemsControl.Items.OfType<TreeViewItem>().ToList().IndexOf(selectedTreeViewItem);
                MoveTreeViewItem(parentItemsControl, selectedTreeViewItem, currentIndex + 1);
            }
        }

        private async void SaveModFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = await SaveFile();
                if (filePath != null)
                {
                    TreeViewItem rootItem = leftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
                    if (rootItem != null)
                    {
                        WriteTreeViewItemsToFile(new List<TreeViewItem> { rootItem }, filePath);
                    }
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
                leftTreeView.Items = new AvaloniaList<object> { rootItem };

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
                TreeViewItem existingItem = parentItem.Items.OfType<TreeViewItem>().FirstOrDefault(item => item.Header.ToString().Equals(component.Name));

                if (existingItem != null)
                {
                    // Update the existing item's tag with the component
                    existingItem.Tag = component;
                    return;
                }

                // Check for duplicate GUID
                Component duplicateComponent = _components.FirstOrDefault(c => c.Guid == component.Guid && c != component);
                if (duplicateComponent != null)
                {
                    Logger.Log($"Mod {component.Name} has duplicate GUID with mod {duplicateComponent.Name}");
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
                    {
                        _ = _selectedComponents.Remove(component);
                    }
                    ItemClickCommand.Execute(component);
                };

                // Add the component item to the parent item
                ((AvaloniaList<object>)parentItem.Items).Add(componentItem);

                // Check if the component has dependencies
                if (component.Dependencies != null && component.Dependencies.Count > 0)
                {
                    // Iterate over the dependencies and create tree view items
                    foreach (string dependencyGuid in component.Dependencies)
                    {
                        try
                        {
                            // Find the dependency in the components list
                            Component dependency = _components.FirstOrDefault(c => c.Guid == dependencyGuid);

                            if (dependency != null)
                            {
                                // Create the dependency tree view item
                                CreateTreeViewItem(dependency, componentItem);
                            }
                        }
                        catch (FormatException ex)
                        {
                            // Usually catches invalid guids for the user
                            Logger.LogException(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception according to your application's requirements
                Logger.LogException(new Exception($"Error creating tree view item: {ex.Message}"));
            }
        }

        private void WriteTreeViewItemsToFile(IEnumerable<TreeViewItem> items, string filePath)
        {
            string randomFileName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName());
            filePath = filePath ?? $"modconfig_{randomFileName}.toml";

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (TreeViewItem item in items)
                {
                    WriteTreeViewItemToFile(item, writer, maxDepth: 1);
                }
            }
        }

        private void WriteTreeViewItemToFile(TreeViewItem item, StreamWriter writer, int depth = 0, int maxDepth = int.MaxValue)
        {
            if (item.Tag is Component component)
            {
                string tomlContents = Serializer.SerializeComponent(component);
                writer.WriteLine(tomlContents);
            }

            if (depth < maxDepth && item.Items != null)
            {
                foreach (TreeViewItem childItem in item.Items.OfType<TreeViewItem>())
                {
                    WriteTreeViewItemToFile(childItem, writer, depth + 1, maxDepth);
                }
            }
        }

        private void RightDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle the selection changed event, if needed
        }
    }

    public class ComboBoxItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string action)
            {
                return new ComboBoxItem { Content = action };
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ComboBoxItem comboBoxItem)
            {
                return comboBoxItem.Content?.ToString();
            }

            return null;
        }
    }

    public class EmptyCollectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ICollection collection && collection.Count == 0)
        {
            // Create a new collection with a default value
            return new List<string>() { string.Empty };
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}



    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> list)
            {
                return string.Join(Environment.NewLine, list);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                return lines.ToList();
            }

            return Enumerable.Empty<string>();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object parameter) => execute(parameter);
    }
    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool isExpanded && targetType == typeof(string) ? isExpanded ? "▼" : "▶" : (object)string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

}
