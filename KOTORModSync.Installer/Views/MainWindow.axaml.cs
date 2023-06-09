// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Avalonia.Controls;
using Prism.Navigation;

namespace KOTORModSync.Installer.Views
{
    public partial class MainWindow : Window
    {
        private readonly INavigationService _navigationService;

        public MainWindow(INavigationService navigationService)
        {
            InitializeComponent();
            _navigationService = navigationService;
            NavigateToWelcome();
        }

        private void NavigateToWelcome()
        {
            _navigationService.NavigateTo("WelcomeView");
        }

    }
}