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
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using System.Diagnostics;
using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Avalonia.Media;

namespace KOTORModSync.GUI
{
    public partial class MainWindow : Window
    {
        private Window _outputWindow;
        private TextBox _logTextBox;
        private StringBuilder _logBuilder;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
            var openFileDialog = new OpenFileDialog();
            openFileDialog.AllowMultiple = false;
            openFileDialog.Filters.Add(new FileDialogFilter { Name = "TOML Files", Extensions = { "toml" } });

            var window = this.FindAncestorOfType<Window>();
            var result = await openFileDialog.ShowAsync(window);

            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                return filePath;
            }

            return null;
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {

                // Open the file dialog to select a file
                var filePath = await OpenFile();

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Verify the file type
                    var fileExtension = Path.GetExtension(filePath);
                    if (string.Equals(fileExtension, ".toml", StringComparison.OrdinalIgnoreCase))
                    {

                        // Clear existing items in the tree view
                        leftTreeView.Items = new Avalonia.Collections.AvaloniaList<object>();

                        // Load components dynamically
                        List<Component> components = Serializer.FileHandler.ReadComponentsFromFile(filePath);

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

                        // Set the root item as the items source of the tree view
                        leftTreeView.Items = (System.Collections.IEnumerable?)rootItem;
                    }
                }
            });
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
                    Header = component.Name
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
    }
}
