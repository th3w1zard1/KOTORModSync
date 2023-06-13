using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core;
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync
{
    public class ComponentViewModel : INotifyPropertyChanged
    {
        private readonly Component _component;
        private bool _isSelected;

        public string Name => _component.Name;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public ComponentViewModel(Component component)
        {
            _component = component;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class SelectInstallComponents : Window
    {
        public List<ComponentViewModel> Components { get; set; }
        public List<ComponentViewModel> SelectedComponents { get; set; }

        public SelectInstallComponents()
        {
            InitializeComponent();

            var components = new List<Component>
            {
                new Component { Name = "Component 1" },
                new Component { Name = "Component 2" },
                new Component { Name = "Component 3" },
                // Add more components as needed
            };

            Components = components.Select(component => new ComponentViewModel(component)).ToList();
            SelectedComponents = new List<ComponentViewModel>();

            DataContext = this;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> selectedComponentNames = SelectedComponents.Select(component => component.Name);
            string message = $"Selected Components: {string.Join(", ", selectedComponentNames)}";

            await InformationDialog.ShowInformationDialog(this, message, "Installation Complete");
            Close();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
