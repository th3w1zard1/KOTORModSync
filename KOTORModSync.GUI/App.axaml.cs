// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace KOTORModSync
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Subscribe to the UnobservedTaskException event
                    TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

                    desktop.MainWindow = new MainWindow();
                    Core.Logger.Log("Started main window");
                }
                catch (Exception ex)
                {
                    Core.Logger.LogException(ex);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Log or handle the unobserved task exception here
            Core.Logger.LogException(e.Exception);
            e.SetObserved(); // Mark the exception as observed to prevent it from crashing the application
        }
    }
}
