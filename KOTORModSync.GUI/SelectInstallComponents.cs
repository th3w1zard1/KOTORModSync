using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core;

namespace KOTORModSync
{
    public partial class SelectInstallComponents : Window
    {
        public SelectInstallComponents()
        {
            InitializeComponent();

            var components = new List<Component>
            {
                new Component { Name = "Component 1" },
                new Component { Name = "Component 2" },
                new Component { Name = "Component 3" }
                // Add more components as needed
            };

            Components = components.ConvertAll( component => new ComponentViewModel( component ) );
            SelectedComponents = new List<ComponentViewModel>();

            DataContext = this;
        }

        public List<ComponentViewModel> Components { get; set; }
        public List<ComponentViewModel> SelectedComponents { get; set; }

        private async void InstallButton_Click( object sender, RoutedEventArgs e )
        {
            IEnumerable<string> selectedComponentNames = SelectedComponents.Select( component => component.Name );
            string message = $"Selected Components: {string.Join( ", ", selectedComponentNames )}";

            await InformationDialog.ShowInformationDialog( this, message, "Installation Complete" );
            Close();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );
    }
}
