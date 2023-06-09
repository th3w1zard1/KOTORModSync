// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KOTORModSync.Installer.ViewModels;
using KOTORModSync.Installer.Views;

namespace KOTORModSync.Installer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Register the navigation service
            services.AddSingleton<INavigationService, NavigationService>();

            // Register your other services here

            base.ConfigureServices(context, services);
        }


    }
}