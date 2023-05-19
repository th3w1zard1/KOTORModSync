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
        private Window _outputWindow;
        private TextBox _logTextBox;
        private StringBuilder _logBuilder;
        private List<Component> components;
        private ObservableCollection<Component> selectedComponents = new ObservableCollection<Component>();
        private ObservableCollection<string> selectedComponentProperties;
        private string originalContent;
        private MainConfig mainConfig;


        private string currentComponent;
        public MainWindow() => InitializeComponent();

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            components = new List<Component>();
            // Find the leftTreeView control and assign it to the member variable
            leftTreeView = this.FindControl<TreeView>("leftTreeView");
            rightTextBox = this.FindControl<TextBox>("rightTextBox");
            rightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            rightTextBox.PropertyChanged += RightTextBox_PropertyChanged;
            rightTextBox.DataContext = selectedComponentProperties;
            selectedComponentProperties = new ObservableCollection<string>();
            guidTextBox = this.FindControl<TextBox>("guidTextBox");
            guidTextBox.Width = rightTextBox.Bounds.Width;
            mainConfig = new MainConfig();
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
            Window? parent = this.VisualRoot as Window;
            if (parent != null)
            {
                string[]? strings = await dialog.ShowAsync(parent);
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
                        components = Serializer.FileHandler.ReadComponentsFromFile(filePath);
                        if (components != null && components.Count > 0)
                        {
                            // Create a dictionary to store components by their GUIDs
                            Dictionary<Guid, Component> componentDictionary = components.ToDictionary(c => Guid.Parse(c.Guid));

                            // Create the root item for the tree view
                            var rootItem = new TreeViewItem
                            {
                                Header = "Components"
                            };

                            // Iterate over the components and create tree view items
                            foreach (var component in components)
                            {
                                CreateTreeViewItem(component, componentDictionary, rootItem);
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

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void AddComponentButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new default component with a new GUID
            var newComponent = new Component
            {
                Guid = Guid.NewGuid().ToString(),
                Name = "new item"
            };

            // Add the new component to the collection
            components.Add(newComponent);

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
                components.Remove(selectedComponent);

                // Refresh the TreeView to reflect the changes
                RefreshTreeView();
            }
        }

        private async Task<string?> OpenFolder()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();

            // Show the dialog and wait for a result.
            Window? parent = this.VisualRoot as Window;
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
                mainConfig.UpdateConfig(modDirectory, kotorInstallDir);
            }
            catch (ArgumentNullException)
            {
                Logger.Log("User cancelled selecting folder");
                return;
            }
        }

        private async void StartInstall_Click(object sender, RoutedEventArgs e)
        {
            if(mainConfig == null || MainConfig.DestinationPath == null)
            {
                var informationDialog = new InformationDialog();
                informationDialog.InfoText = "Please set your directories first";
                await informationDialog.ShowDialog<bool?>(this);
            }
            foreach(var component in components)
            {
                var confirmationDialogCallback = new ConfirmationDialogCallback(this);
                // Call the ExecuteInstructions method and pass the confirmationDialogCallback
                await Component.ExecuteInstructions(confirmationDialogCallback, components);
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
                if (!selectedComponents.Contains(component))
                {
                    selectedComponents.Add(component);
                }
                PopulateRightTextBox(component);
            }
        }

        private void PopulateRightTextBox(Core.Component selectedComponent)
        {
            if (selectedComponent != null && rightTextBox != null)
            {
                originalContent = Serializer.SerializeComponent(selectedComponent);
                rightTextBox.Text = originalContent;
            }
        }

        private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            // Generate a unique GUID
            Guid uniqueGuid = Guid.NewGuid();

            // Set the generated GUID to the guidTextBox
            guidTextBox.Text = "{" + uniqueGuid.ToString() + "}";
        }

        private void RightListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }
        private bool CheckForChanges()
        {
            string currentContent = rightTextBox.Text;
            return !string.Equals(currentContent, originalContent);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool hasChanges = CheckForChanges();

            if (hasChanges)
            {
                bool confirmationResult = await ConfirmationDialog.ShowConfirmationDialog(this, "Are you sure you want to save?");
                if (confirmationResult)
                {
                    bool result = SaveChanges();
                    var informationDialog = new InformationDialog();
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
                        int index = components.IndexOf(selectedComponent);
                        if (index >= 0)
                        {
                            // Update the properties of the component
                            components[index] = newComponent;
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
            var components = parentItemsControl.Items.OfType<TreeViewItem>().ToList();
            int currentIndex = components.IndexOf(selectedTreeViewItem);

            if (currentIndex != -1 && newIndex >= 0 && newIndex < components.Count)
            {
                components.RemoveAt(currentIndex);
                components.Insert(newIndex, selectedTreeViewItem);
                leftTreeView.SelectedItem = selectedTreeViewItem;
                RefreshTreeView();
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

        private void RefreshTreeView()
        {
            // Create a dictionary to store components by their GUIDs
            Dictionary<Guid, Core.Component> componentDictionary;
            try
            {
                componentDictionary = components.ToDictionary(c => Guid.Parse(c.Guid));
            }
            catch(Exception ex)
            {
                if (ex is ArgumentException || ex is FormatException)
                    Logger.LogException(ex);
                else
                    throw;
                return;
            }
            // Create the root item for the tree view
            var rootItem = new TreeViewItem
            {
                Header = "Components",
            };

            // Iterate over the components and create tree view items
            foreach (var component in components)
            {
                CreateTreeViewItem(component, componentDictionary, rootItem);
            }

            // Create a collection to hold the root item
            var rootItemsCollection = new AvaloniaList<TreeViewItem> { rootItem };

            // Set the root item collection as the items source of the tree view
            leftTreeView.Items = rootItemsCollection;

            // Expand the root item to automatically expand the tree view
            rootItem.IsExpanded = true;

            WriteTreeViewItemsToFile(rootItem);
        }

        private void CreateTreeViewItem(Component component, Dictionary<Guid, Component> componentDictionary, TreeViewItem parentItem)
        {
            try
            {
                // Check if the component item is already added to the parent item
                if (parentItem.Items.Cast<TreeViewItem>().Any(item => item.Header.ToString().Equals(component.Name)))
                    return;

                // Create a new tree view item for the component
                var componentItem = new TreeViewItem
                {
                    Header = component.Name,
                    Tag = component // this allows us to access the item later
                };

                // Assign the ItemClickCommand to the componentItem
                componentItem.DoubleTapped += (sender, e) =>
                {
                    if (selectedComponents.Contains(component))
                    {
                        selectedComponents.Remove(component);
                    }
                    ItemClickCommand.Execute(component);
                };

                // Add the component item to the parent item
                ((AvaloniaList<object>)parentItem.Items).Add(componentItem);

                // Check if the component has dependencies
                if (component.Dependencies != null && component.Dependencies.Count > 0)
                {
                    // Iterate over the dependencies and create tree view items recursively
                    foreach (var dependencyGuid in component.Dependencies)
                    {
                        try
                        {
                            // Check if the dependency exists in the component dictionary
                            if (componentDictionary.TryGetValue(Guid.Parse(dependencyGuid), out Component dependency))
                            {
                                // Create the dependency tree view item recursively
                                CreateTreeViewItem(dependency, componentDictionary, componentItem);
                            }
                        }
                        catch (FormatException ex)
                        {
                            // usually catches invalid guids for the user.
                            Logger.LogException(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception according to your application's requirements
                Logger.LogException(new Exception("Error creating tree view item: {ex.Message}"));
            }
        }

        private void WriteTreeViewItemsToFile(TreeViewItem rootItem)
        {
            string randomFileName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName());
            string filePath = $"modconfig_{randomFileName}.toml";

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write the tree view items to the file
                WriteTreeViewItemToFile(rootItem, writer);
            }
        }


        private void WriteTreeViewItemToFile(TreeViewItem item, StreamWriter writer)
        {
            var component = item.Tag as Component;
            if (component != null)
            {
                var tomlContents = Serializer.SerializeComponent(component);
                writer.WriteLine(tomlContents);
            }

            // Process child items recursively
            foreach (var childItem in item.Items.OfType<TreeViewItem>())
            {
                WriteTreeViewItemToFile(childItem, writer);
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

        public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object parameter) => execute(parameter);

        public event EventHandler CanExecuteChanged;
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
