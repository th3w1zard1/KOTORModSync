// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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
            this.ProgressTextBlock = this.FindControl<TextBlock>( "ProgressTextBlock" );
            this.ProgressBar = this.FindControl<ProgressBar>( "ProgressBar" );
            this.InstalledRemaining = this.FindControl<TextBlock>( "InstalledRemaining" );
            this.PercentCompleted = this.FindControl<TextBlock>( "PercentCompleted" );
        }

        public static async Task ShowProgressWindow( Window parentWindow, string message, decimal progress )
        {
            var progressWindow = new ProgressWindow { Owner = parentWindow };
            progressWindow.ProgressTextBlock.Text = message;
            progressWindow.ProgressBar.Value = (double)progress;

            _ = await progressWindow.ShowDialog<bool?>( parentWindow );
        }

        public void Dispose() => Close();
    }
}
