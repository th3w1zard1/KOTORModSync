using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Data;
using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using System.Collections.ObjectModel;
using static Nett.TomlObjectFactory;
using Avalonia.Data.Converters;
using System.Globalization;

namespace KOTORModSync.GUI
{
    public partial class MainWindow : Window
    {
        private List<Component> _components;
        private ObservableCollection<Component> _selectedComponents = new ObservableCollection<Component>();
        private ObservableCollection<string> _selectedComponentProperties;
        private string _originalContent;
        private MainConfig _mainConfig;
        private Component currentComponent;
        public MainWindow() => InitializeComponent();

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _components = new List<Component>();
            // Find the leftTreeView control and assign it to the member variable
            leftTreeView = this.FindControl<TreeView>("leftTreeView");
            rightTextBox = this.FindControl<TextBox>("rightTextBox");
            rightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            rightTextBox.PropertyChanged += (f, f2) => RightTextBox_PropertyChanged(f, f2);
            rightTextBox.DataContext = _selectedComponentProperties;
            _selectedComponentProperties = new ObservableCollection<string>();
            guidTextBox = this.FindControl<TextBox>("guidTextBox");
            guidTextBox.Width = rightTextBox.Bounds.Width;
            _mainConfig = new MainConfig();
        }

        private void RightTextBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.BoundsProperty)
            {
                guidTextBox.Width = rightTextBox.Bounds.Width;
            }
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

                    foreach (var dependency in component.Dependencies)
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
                    foreach (var childItem in childItems.Values)
                    {
                        items.Add(childItem);
                    }

                    return treeViewItem;
                }
            }

            // Return null if the item is not a Component
            return null;
        }

        private async Task<string> OpenFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.AllowMultiple = false;
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "Mod Sync File", Extensions = { "toml", "tml" } });
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });

            // Show the dialog and wait for a result.
            Window parent = this.VisualRoot as Window;
            if (parent != null)
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

        private async Task<string> SaveFile()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.DefaultExtension = "toml";
            dialog?.Filters?.Add(new FileDialogFilter() { Name = "Mod Sync File", Extensions = { "toml", "tml" } });

            // Show the dialog and wait for a result.
            Window parent = this.VisualRoot as Window;
            if (parent != null)
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
            var filePath = await OpenFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                // Verify the file type
                var fileExtension = System.IO.Path.GetExtension(filePath);
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
                            foreach (var component in _components)
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
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please set your directories first";
                await informationDialog.ShowDialog<bool?>(this);
            }
            return;
        }

        private void AddComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new default component with a new GUID
            var newComponent = new Component
            {
                Guid = Guid.NewGuid().ToString(),
                Name = ("new mod_" + Path.GetRandomFileName())
            };

            // Add the new component to the collection
            _components.Add(newComponent);

            // Refresh the TreeView to reflect the changes
            RefreshTreeView();

            // Set the example deserialized string for the new component
            string exampleString = Component.defaultComponent += Instruction.defaultInstructions;
            rightTextBox.Text = exampleString;
        }

        private void RemoveComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected component from the TreeView
            if (leftTreeView.SelectedItem is TreeViewItem selectedTreeViewItem && selectedTreeViewItem.Tag is Component selectedComponent)
            {
                // Remove the selected component from the collection
                _components.Remove(selectedComponent);

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
        }

        private void RefreshComponents_Click(object sender, RoutedEventArgs e)
        {
            RefreshTreeView();
        }

        private async Task<string> OpenFolder()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();

            // Show the dialog and wait for a result.
            Window parent = this.VisualRoot as Window;
            if (parent != null)
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
            var informationDialog = new InformationDialog();
            informationDialog.InfoText = "Please select your mod directory (where the archives are).";
            await informationDialog.ShowDialog<bool?>(this);
            try
            {
                var chosenFolder = await OpenFolder();
                DirectoryInfo modDirectory = new DirectoryInfo(chosenFolder);
                informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please select your KOTOR(2) directory. (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")";
                await informationDialog.ShowDialog<bool?>(this);
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
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please set your directories first";
                await informationDialog.ShowDialog<bool?>(this);
                return;
            }
            var selectedTreeViewItem = leftTreeView.SelectedItem as TreeViewItem;
            Component thisComponent;
            if (selectedTreeViewItem != null && selectedTreeViewItem.Tag is Component selectedComponent)
                thisComponent = (Component)selectedTreeViewItem.Tag;
            else
            {
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please choose a mod to install from the left list first";
                await informationDialog.ShowDialog<bool?>(this);
                return;
            }
            var confirmationDialogCallback = new ConfirmationDialogCallback(this);

            // Call the ExecuteInstructions method asynchronously using Task.Run
            var result = await Task.Run(() => thisComponent.ExecuteInstructions(confirmationDialogCallback, _components));
            if (!result.success)
            {
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = $"There was a problem installing {thisComponent.Name}, please check the output window";
                await informationDialog.ShowDialog<bool?>(this);
                return;
            }
            else
            {
                Logger.Log($"Successfully installed {thisComponent.Name}");
            }
        }

        private async void StartInstall_Click(object sender, RoutedEventArgs e)
        {
            bool confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, "yo man it's not a bait. This button will install all mods sequentially without stopping until the end is reached. If you don't want this, press no");
            if (!confirmationResult)
                return;
            if (_mainConfig == null || MainConfig.DestinationPath == null)
            {
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please set your directories first";
                await informationDialog.ShowDialog<bool?>(this);
                return;
            }

            foreach (var component in _components)
            {
                var confirmationDialogCallback = new ConfirmationDialogCallback(this);

                // Call the ExecuteInstructions method asynchronously using Task.Run
                var result = await Task.Run(() => component.ExecuteInstructions(confirmationDialogCallback, _components));

                if (!result.success)
                {
                    confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, $"There was a problem installing {component.Name}, please check the output window. Continue with next mod anyway?");
                    if (!confirmationResult)
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


        private ICommand itemClickCommand;

        public ICommand ItemClickCommand
        {
            get { return itemClickCommand ?? (itemClickCommand = new RelayCommand(ItemClick)); }
        }

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

        private void PopulateRightTextBox(Core.Component selectedComponent)
        {
            if (selectedComponent != null && rightTextBox != null)
            {
                _originalContent = Serializer.SerializeComponent(selectedComponent);
                rightTextBox.Text = _originalContent;
                this.currentComponent = selectedComponent;
            }
        }

        private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            // Generate a unique GUID
            Guid uniqueGuid = Guid.NewGuid();

            // Set the generated GUID to the guidTextBox
            guidTextBox.Text = "{" + uniqueGuid.ToString().ToUpper() + "}";
        }

        private void RightListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }
        private bool CheckForChanges()
        {
            string currentContent = rightTextBox.Text;
            return !string.Equals(currentContent, _originalContent);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {   if (currentComponent is null)
                currentComponent = leftTreeView.SelectedItem as Core.Component;
            if (currentComponent is null)
            {
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "You must select a component from the list, or create one, before saving.";
                await informationDialog.ShowDialog<bool?>(this);
            }
            else if (CheckForChanges())
            {
                bool confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, "Are you sure you want to save?");
                if (confirmationResult)
                {
                    var informationDialog = new InformationDialog();
                    bool result = SaveChanges();
                    if (result)
                    {
                        informationDialog.InfoText = "Saved successfully. Check the output window for more information.";
                    }
                    else
                    {
                        informationDialog.InfoText = "There were some problems with your syntax, please check the output window.";
                    }
                    await informationDialog.ShowDialog<bool?>(this);
                    RefreshTreeView();
                }
            }
        }

        public bool SaveChanges()
        {
            // Get the selected component from the tree view
            var selectedTreeViewItem = leftTreeView.SelectedItem as TreeViewItem;
            if (selectedTreeViewItem != null && selectedTreeViewItem.Tag is Component selectedComponent)
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
                var componentsList = _components; // Use the original components list
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
            catch(ArgumentOutOfRangeException ex)
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
                var filePath = await SaveFile();
                if (filePath != null)
                {
                    var rootItem = leftTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
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
                foreach (var component in _components)
                {
                    CreateTreeViewItem(component, rootItem);
                }

                // Set the root item as the single item of the tree view
                leftTreeView.Items = new AvaloniaList<object> { rootItem };

                // Expand the root item to automatically expand the tree view
                rootItem.IsExpanded = true;

                WriteTreeViewItemsToFile(new List<TreeViewItem> { rootItem }, null);
                currentComponent = null;
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
                var existingItem = parentItem.Items.OfType<TreeViewItem>().FirstOrDefault(item => item.Header.ToString().Equals(component.Name));

                if (existingItem != null)
                {
                    // Update the existing item's tag with the component
                    existingItem.Tag = component;
                    return;
                }

                // Check for duplicate GUID
                var duplicateComponent = _components.FirstOrDefault(c => c.Guid == component.Guid && c != component);
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
                        _selectedComponents.Remove(component);
                    }
                    ItemClickCommand.Execute(component);
                };

                // Add the component item to the parent item
                ((AvaloniaList<object>)parentItem.Items).Add(componentItem);

                // Check if the component has dependencies
                if (component.Dependencies != null && component.Dependencies.Count > 0)
                {
                    // Iterate over the dependencies and create tree view items
                    foreach (var dependencyGuid in component.Dependencies)
                    {
                        try
                        {
                            // Find the dependency in the components list
                            var dependency = _components.FirstOrDefault(c => c.Guid == dependencyGuid);

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
                foreach (var item in items)
                {
                    WriteTreeViewItemToFile(item, writer, maxDepth: 1);
                }
            }
        }

        private void WriteTreeViewItemToFile(TreeViewItem item, StreamWriter writer, int depth = 0, int maxDepth = int.MaxValue)
        {
            var component = item.Tag as Component;
            if (component != null)
            {
                var tomlContents = Serializer.SerializeComponent(component);
                writer.WriteLine(tomlContents);
            }

            if (depth < maxDepth && item.Items != null)
            {
                foreach (var childItem in item.Items.OfType<TreeViewItem>())
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
    public class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object parameter) => execute(parameter);
    }
    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && targetType == typeof(string))
            {
                return isExpanded ? "▼" : "▶";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
