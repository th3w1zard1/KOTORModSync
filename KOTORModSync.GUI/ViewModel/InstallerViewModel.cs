using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.ViewModel
{
    public class InstallerViewModel : UserControl
    {
        private readonly List<Component> _allComponents;
        private ConfirmationScreenViewModel _confirmationScreenViewModel;
        private InstallationProgressScreenViewModel _installationProgressScreenViewModel;
        private ResultsScreenViewModel _resultsScreenViewModel;
        private SelectComponentsViewModel _selectComponentsViewModel;

        public InstallerViewModel( List<Component> availableComponents )
        {
            InitializeComponent();
            InitializeViewModels();
            DataContext = _selectComponentsViewModel;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void InitializeViewModels()
        {
            _selectComponentsViewModel = new SelectComponentsViewModel();
            _confirmationScreenViewModel = new ConfirmationScreenViewModel();
            _installationProgressScreenViewModel = new InstallationProgressScreenViewModel();
            _resultsScreenViewModel = new ResultsScreenViewModel();
        }

        private void ShowScreen( object screenViewModel ) => DataContext = screenViewModel;

        private void NextButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case SelectComponentsViewModel _:
                    ShowScreen( _confirmationScreenViewModel );
                    break;
                case ConfirmationScreenViewModel _:
                    ShowScreen( _installationProgressScreenViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( _resultsScreenViewModel );
                    break;
            }
            // Handle navigation to additional screens if needed
        }

        private void BackButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case ConfirmationScreenViewModel _:
                    ShowScreen( _selectComponentsViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( _confirmationScreenViewModel );
                    break;
                case ResultsScreenViewModel _:
                    ShowScreen( _installationProgressScreenViewModel );
                    break;
            }
            // Handle navigation to previous screens if needed
        }
    }
}