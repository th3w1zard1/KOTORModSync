using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync.GUI
{
    public partial class InformationDialog : Window
    {
        public InformationDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static readonly AvaloniaProperty InfoTextProperty =
            AvaloniaProperty.Register<InformationDialog, string>("InfoText");

        public string InfoText
        {
            get => GetValue(InfoTextProperty) as string;
            set => SetValue(InfoTextProperty, value);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            UpdateInfoText();
        }

        private void UpdateInfoText()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var textBlock = this.FindControl<TextBlock>("InfoTextBlock");
                textBlock.Text = InfoText;
            });
        }
    }
}