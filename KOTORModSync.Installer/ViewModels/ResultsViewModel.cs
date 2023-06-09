using Prism.Mvvm;
using Prism.Commands;
using Prism.Regions;

namespace KOTORModSync.Installer.ViewModels
{
    public class ResultsViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;

        private string _resultsText;

        public string ResultsText
        {
            get => _resultsText;
            set => SetProperty(ref _resultsText, value);
        }

        public DelegateCommand FinishCommand { get; }

        public ResultsViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            FinishCommand = new DelegateCommand(FinishInstallation);
        }

        private void FinishInstallation()
        {
            // TODO: Implement the logic to finish the installation and close the application.
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // TODO: Implement any necessary initialization or data loading when navigating to this view.
            // You can set the ResultsText property here to display the results to the user.
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // TODO: Return true if this view can handle the navigation request, or false if a new instance is required.
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // TODO: Perform any cleanup or data saving when navigating away from this view.
        }
    }
}