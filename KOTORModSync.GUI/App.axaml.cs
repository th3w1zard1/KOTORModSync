// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core;

namespace KOTORModSync.GUI
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
                try
                {
                    var mainWindow = new MainWindow();
                    desktop.MainWindow = mainWindow;

                    var outputWindow = new OutputWindow();
                    outputWindow.Show();
                    Core.Logger.Log("Started main window");
                }
                catch(System.Exception ex)
                {
                    Core.Logger.LogException(ex);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}
