// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace KOTORModSync.Installer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public static string Greeting => "Welcome to Avalonia!";
        public object? CurrentScreen { get; }
    }
}
