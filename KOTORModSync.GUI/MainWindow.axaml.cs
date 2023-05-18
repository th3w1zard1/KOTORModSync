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
using Avalonia.Controls.Templates;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using System.Diagnostics;
using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Avalonia.Media;
using static System.Net.WebRequestMethods;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Services.Common;
using System.Reflection;
using Avalonia.Controls.Notifications;
using static Nett.TomlObjectFactory;
using Avalonia.Controls.Shapes;

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


        private string currentComponent;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            // Find the leftTreeView control and assign it to the member variable
            leftTreeView = this.FindControl<TreeView>("leftTreeView");
            rightTextBox = this.FindControl<TextBox>("rightTextBox");
            rightTextBox.LostFocus += RightListBox_LostFocus; // Prevents rightListBox from being cleared when clicking elsewhere.
            selectedComponentProperties = new ObservableCollection<string>();
            rightTextBox.DataContext = selectedComponentProperties;
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
            dialog.Filters.Add(new FileDialogFilter() { Name = "Mod Sync File", Extensions = { "toml" } });
            dialog.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });

            // Show the dialog and wait for a result.
            Window parent = this.VisualRoot as Window;
            if (parent != null)
            {
                string[] files = await dialog.ShowAsync(parent);
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



        private async void Button1_Click(object sender, RoutedEventArgs e)
        {
            // Open the file dialog to select a file
            var filePath = await OpenFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                // Verify the file type
                var fileExtension = System.IO.Path.GetExtension(filePath);
                if (string.Equals(fileExtension, ".toml", StringComparison.OrdinalIgnoreCase))
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

        private ICommand itemClickCommand;

        public ICommand ItemClickCommand
        {
            get { return itemClickCommand ?? (itemClickCommand = new RelayCommand(ItemClick)); }
        }

        private void ItemClick(object parameter)
        {
            if (parameter is Component component)
            {
                // Handle the item click event here
                if (!selectedComponents.Contains(component))
                {
                    selectedComponents.Add(component);
                }
                PopulateRightTextBox(component);
            }
        }

        private void PopulateRightTextBox(Component selectedComponent)
        {
            if (selectedComponent != null && rightTextBox != null)
            {
                originalContent = Serializer.SerializeComponent(selectedComponent);
                rightTextBox.Text = originalContent;
            }
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
                        informationDialog.InfoText = "Saved successfully";
                    }
                    else
                    {
                        informationDialog.InfoText = "There were some problems with your syntax, please check the output window.";
                    }
                    await informationDialog.ShowDialog<bool?>(this);
                }
            }
        }

        public bool SaveChanges()
        {
            // Get the selected component from the tree view
            var selectedTreeViewItem = leftTreeView.SelectedItem as TreeViewItem;
            if (selectedTreeViewItem != null && selectedTreeViewItem.Tag is Component selectedComponent)
            {
                Component newComponent = Serializer.FileHandler.DeserializeTomlComponent(rightTextBox.Text);
                if (newComponent != null)
                    return true;
                //SaveComponentsToFile();
                return false;
            }

            Logger.LogException(new Exception("Original component is null somehow"));
            return false;
        }


        private void RightListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
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
                        // Check if the dependency exists in the component dictionary
                        if (componentDictionary.TryGetValue(Guid.Parse(dependencyGuid), out Component dependency))
                        {
                            // Create the dependency tree view item recursively
                            CreateTreeViewItem(dependency, componentDictionary, componentItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception according to your application's requirements
                Console.WriteLine($"Error creating tree view item: {ex.Message}");
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

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            execute(parameter);
        }

        public event EventHandler CanExecuteChanged;
    }

}
