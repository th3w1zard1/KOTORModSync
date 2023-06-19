// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync.GUI
{
    public class ComponentViewModel : INotifyPropertyChanged
    {
        private readonly Component _component;
        private bool _isSelected;

        public ComponentViewModel( Component component ) => _component = component;

        public string Name => _component.Name;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if ( _isSelected == value )
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged( nameof( IsSelected ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged
            ( string propertyName ) => PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }

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
