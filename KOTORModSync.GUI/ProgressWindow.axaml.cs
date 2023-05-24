using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.GUI
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
            AttachControls();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AttachControls()
        {
            progressTextBlock = this.FindControl<TextBlock>("progressTextBlock");
            progressBar = this.FindControl<ProgressBar>("progressBar");
        }

        public static async Task ShowProgressWindow(Window parentWindow, string message, decimal progress)
        {
            var progressWindow = new ProgressWindow { Owner = parentWindow };
            progressWindow.progressTextBlock.Text = message;
            progressWindow.progressBar.Value = (double)progress;

            await progressWindow.ShowDialog<bool?>(parentWindow);
        }
        public void Dispose()
        {
            Close();
        }
    }
}
