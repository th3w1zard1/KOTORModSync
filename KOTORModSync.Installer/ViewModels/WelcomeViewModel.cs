using System;
using System.Collections.Generic;
using Prism.Mvvm;
using Prism.Commands;

namespace KOTORModSync.Installer.ViewModels
{
    public class WelcomeViewModel : BindableBase
    {
        private string _welcomeMessage;
        private bool _isInstructionsLoaded;
        private string _selectedDirectories;
        private INavigationService _navigationService;

        public WelcomeViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            WelcomeMessage = "KOTORModSync Installer!";
            LoadInstructionsCommand = new DelegateCommand(LoadInstructions);
            SelectDirectoriesCommand = new DelegateCommand(SelectDirectories);
            NextCommand = new DelegateCommand(NavigateToSelectComponents);
        }

        private void LoadInstructions()
        {
            // Add your logic here for loading instructions
            // This method will be executed when the LoadInstructionsCommand is invoked
        }

        private void SelectDirectories()
        {
            // Add your logic here for selecting directories
            // This method will be executed when the SelectDirectoriesCommand is invoked
        }

        private void Next()
        {
            // Add your logic here for navigating to the next page
            // This method will be executed when the NextCommand is invoked
        }

        private async void NavigateToSelectComponents()
        {
            // Add your logic here for any necessary validation or preparation before navigating

            await _navigationService.NavigateAsync("SelectComponentsView");
        }

        public bool IsInstructionsLoaded
        {
            get => _isInstructionsLoaded;
            set => SetProperty(ref _isInstructionsLoaded, value);
        }

        public string SelectedDirectories
        {
            get => _selectedDirectories;
            set => SetProperty(ref _selectedDirectories, value);
        }

        public DelegateCommand LoadInstructionsCommand { get; }
        public DelegateCommand SelectDirectoriesCommand { get; }
        public DelegateCommand NextCommand { get; }

        public WelcomeViewModel()
        {
            WelcomeMessage = "Welcome to the Installer Application!";
            LoadInstructionsCommand = new DelegateCommand(LoadInstructions);
            SelectDirectoriesCommand = new DelegateCommand(SelectDirectories);
            NextCommand = new DelegateCommand(Next);
        }
    }
}