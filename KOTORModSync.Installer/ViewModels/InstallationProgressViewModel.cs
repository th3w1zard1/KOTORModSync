using System.Threading.Tasks;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace KOTORModSync.Installer.ViewModels
{
    public class InstallationProgressViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;

        private string _progressText;

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty( ref _progressText, value );
        }

        public DelegateCommand BackCommand { get; }
        public DelegateCommand NextCommand { get; }

        public InstallationProgressViewModel( IRegionManager regionManager, string progressText )
        {
            _regionManager = regionManager;
            _progressText = progressText;

            ProgressText = "Installation in progress...";

            BackCommand = new DelegateCommand( NavigateBack );
            NextCommand = new DelegateCommand( NavigateNext );
        }

        private void NavigateBack()
        {
            // TODO: Implement the logic to navigate back to the previous screen (ConfirmationView).
        }

        private void NavigateNext()
        {
            // TODO: Implement the logic to navigate to the next screen (ResultsView).
        }

        private static async Task PerformInstallation()
        {
            // TODO: Implement the installation logic.
            // You can use async/await or any other approach to handle the installation process.
            // Update the ProgressText property to display progress and diagnostics to the user.
        }

        public void OnNavigatedTo( NavigationContext navigationContext )
        {
            // TODO: Implement any necessary initialization or data loading when navigating to this view.
            // You can call the PerformInstallation method here to start the installation process.
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
