using System.Collections.Generic;
using KOTORModSync.Core;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace KOTORModSync.Installer.ViewModels
{
    public class ConfirmationViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly InstallationProgressViewModel _installationProgressViewModel;

        private string _confirmationMessage;
        private List<Component> _componentsToInstall;
        private Component _selectedComponent;

        public string ConfirmationMessage
        {
            get => _confirmationMessage;
            set => SetProperty( ref _confirmationMessage, value );
        }

        public List<Component> ComponentsToInstall
        {
            get => _componentsToInstall;
            set => SetProperty( ref _componentsToInstall, value );
        }

        public Component SelectedComponent
        {
            get => _selectedComponent;
            set => SetProperty( ref _selectedComponent, value );
        }

        public DelegateCommand BackCommand { get; }
        public DelegateCommand NextCommand { get; }

        public ConfirmationViewModel( IRegionManager regionManager, InstallationProgressViewModel installationProgressViewModel, string confirmationMessage )
        {
            _regionManager = regionManager;
            _installationProgressViewModel = installationProgressViewModel;
            _confirmationMessage = confirmationMessage;

            ConfirmationMessage = "Please review the selected components before proceeding.";

            BackCommand = new DelegateCommand( NavigateBack );
            NextCommand = new DelegateCommand( NavigateNext );
        }

        public ConfirmationViewModel()
        {

        }

        private void NavigateBack()
        {
            // TODO: Implement the logic to navigate back to the previous screen (SelectComponentsView).
        }

        private void NavigateNext()
        {
            // TODO: Implement the logic to navigate to the next screen (InstallationView).
        }

        public void OnNavigatedTo( NavigationContext navigationContext )
        {
            // TODO: Implement any necessary initialization or data loading when navigating to this view.
        }

        public bool IsNavigationTarget( NavigationContext navigationContext )
        {
            // TODO: Return true if this view can handle the navigation request, or false if a new instance is required.
            return true;
        }

        public void OnNavigatedFrom( NavigationContext navigationContext )
        {
            // TODO: Perform any cleanup or data saving when navigating away from this view.
        }
    }
}
