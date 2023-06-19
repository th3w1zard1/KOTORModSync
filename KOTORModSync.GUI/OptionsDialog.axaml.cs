// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
    internal sealed class OptionsDialogCallback : CallbackObjects.IOptionsDialogCallback
    {
        private readonly Window _topLevelWindow;

        public OptionsDialogCallback( Window topLevelWindow ) => _topLevelWindow = topLevelWindow;

        public Task<string> ShowOptionsDialog
            ( List<string> options ) => OptionsDialog.ShowOptionsDialog( _topLevelWindow, options );
    }

    public partial class OptionsDialog : Window
    {
        public static readonly AvaloniaProperty OptionsListProperty =
            AvaloniaProperty.Register<OptionsDialog, List<string>>( nameof( OptionsList ) );

        public OptionsDialog()
        {
            InitializeComponent();
            OptionTextBox = this.FindControl<TextBlock>( "OptionTextBox" );
            OptionStackPanel = this.FindControl<StackPanel>( "OptionStackPanel" );
            OkButton = this.FindControl<Button>( "OkButton" );
            OkButton.Click += OKButton_Click;
        }

        [CanBeNull]
        public List<string> OptionsList
        {
            get => GetValue( OptionsListProperty ) as List<string>;
            set => SetValue( OptionsListProperty, value );
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void OKButton_Click( object sender, RoutedEventArgs e )
        {
            RadioButton selectedRadioButton = OptionStackPanel
                .Children
                .OfType<RadioButton>()
                .SingleOrDefault( rb => rb.IsChecked == true );

            if ( selectedRadioButton != null )
            {
                string selectedOption = selectedRadioButton.Content?.ToString();
                OptionSelected?.Invoke( this, selectedOption );
            }

            Close();
        }

        public event EventHandler<string> OptionSelected;

        private void OnOpened( object sender, EventArgs e )
        {
            StackPanel optionStackPanel = this.FindControl<StackPanel>( "OptionStackPanel" );

            foreach ( string option in OptionsList )
            {
                var radioButton = new RadioButton { Content = option, GroupName = "OptionsGroup" };
                optionStackPanel.Children.Add( radioButton );
            }

            // Measure and arrange the optionStackPanel to update DesiredSize
            optionStackPanel.Measure( new Size( double.PositiveInfinity, double.PositiveInfinity ) );
            optionStackPanel.Arrange( new Rect( optionStackPanel.DesiredSize ) );

            // Get the actual size of the optionStackPanel including children and transformations
            Size actualSize = optionStackPanel.Bounds.Size;

            // Define padding values
            double horizontalPadding = 100; // Padding on the left and right
            double verticalPadding = 150; // Padding on the top and bottom

            // Calculate the desired width and height for the content with padding
            double contentWidth = actualSize.Width + 2 * horizontalPadding;
            double contentHeight = actualSize.Height + 2 * verticalPadding;

            // Set the width and height of the window
            Width = contentWidth;
            Height = contentHeight;

            InvalidateArrange();
            InvalidateMeasure();

            // Center the window on the screen
            Screen screen = Screens.ScreenFromVisual( this );
            double screenWidth = screen.Bounds.Width;
            double screenHeight = screen.Bounds.Height;
            double left = ( screenWidth - contentWidth ) / 2;
            double top = ( screenHeight - contentHeight ) / 2;
            Position = new PixelPoint( (int)left, (int)top );
        }

        public static async Task<string> ShowOptionsDialog( Window parentWindow, List<string> optionsList )
        {
            var tcs = new TaskCompletionSource<string>();

            await Dispatcher.UIThread.InvokeAsync(
                async () =>
                {
                    var optionsDialog = new OptionsDialog { OptionsList = optionsList };

                    optionsDialog.Closed += ClosedHandler;
                    optionsDialog.Opened += optionsDialog.OnOpened;

                    void ClosedHandler( object sender, EventArgs e )
                    {
                        optionsDialog.Closed -= ClosedHandler;
                        _ = tcs.TrySetResult( null );
                    }

                    optionsDialog.OptionSelected += ( sender, option ) => { _ = tcs.TrySetResult( option ); };

                    await optionsDialog.ShowDialog( parentWindow );
                }
            );

            return await tcs.Task;
        }
    }
}
