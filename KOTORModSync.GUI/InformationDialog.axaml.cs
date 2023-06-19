// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync
{
    public partial class InformationDialog : Window
    {
        public static readonly AvaloniaProperty InfoTextProperty =
            AvaloniaProperty.Register<InformationDialog, string>( "InfoText" );

        public InformationDialog() => InitializeComponent();

        public string InfoText
        {
            get => GetValue( InfoTextProperty ) as string;
            set => SetValue( InfoTextProperty, value );
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        public static async Task ShowInformationDialog
            ( Window parentWindow, string message, string title = "Information" )
        {
            var dialog = new InformationDialog { Title = title, InfoText = message };
            _ = await dialog.ShowDialog<bool?>( parentWindow );
        }

        protected override void OnOpened( EventArgs e )
        {
            base.OnOpened( e );
            UpdateInfoText();
        }

        private void OKButton_Click( object sender, RoutedEventArgs e ) => Close();

        private void UpdateInfoText() => Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                TextBlock textBlock = this.FindControl<TextBlock>( "InfoTextBlock" );
                textBlock.Text = InfoText;
            }
        );
    }
}
