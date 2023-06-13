// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
using System;
After:
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT System;
*/
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
            AttachControls();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void AttachControls()
        {
            progressTextBlock = this.FindControl<TextBlock>( "progressTextBlock" );
            progressBar = this.FindControl<ProgressBar>( "progressBar" );
        }

        public static async Task ShowProgressWindow( Window parentWindow, string message, decimal progress )
        {
            var progressWindow = new ProgressWindow { Owner = parentWindow };
            progressWindow.progressTextBlock.Text = message;
            progressWindow.progressBar.Value = (double)progress;

            _ = await progressWindow.ShowDialog<bool?>( parentWindow );
        }
        public void Dispose() => Close();
    }
}
