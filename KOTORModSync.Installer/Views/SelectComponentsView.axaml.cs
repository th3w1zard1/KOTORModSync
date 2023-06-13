// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Installer.Views
{
    public partial class SelectComponentsView : UserControl
    {
        public SelectComponentsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );
        }
    }
}
