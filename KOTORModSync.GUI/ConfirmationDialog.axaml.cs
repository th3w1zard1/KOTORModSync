// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;

namespace KOTORModSync
{
    public partial class ConfirmationDialog : Window
    {
        public static readonly AvaloniaProperty s_confirmTextProperty
            = AvaloniaProperty.Register<ConfirmationDialog, string>( nameof( ConfirmText ) );

        private static readonly RoutedEvent<RoutedEventArgs> s_yesButtonClickedEvent
            = RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
                nameof( YesButtonClicked ),
                RoutingStrategies.Bubble
            );

        private static readonly RoutedEvent<RoutedEventArgs> s_noButtonClickedEvent
            = RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
                nameof( NoButtonClicked ),
                RoutingStrategies.Bubble
            );

        public ConfirmationDialog() => InitializeComponent();

        [CanBeNull]
        public string ConfirmText
        {
            get => GetValue( s_confirmTextProperty ) as string;
            set => SetValue( s_confirmTextProperty, value );
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void OnOpened( [CanBeNull] object sender, [CanBeNull] EventArgs e )
        {
            TextBlock confirmTextBlock = this.FindControl<TextBlock>( "ConfirmTextBlock" );
            confirmTextBlock.Text = ConfirmText;
        }

        public static async Task<bool?> ShowConfirmationDialog(
            [CanBeNull] Window parentWindow,
            [CanBeNull] string confirmText
        )
        {
            var tcs = new TaskCompletionSource<bool?>();

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var confirmationDialog = new ConfirmationDialog
                    {
                        ConfirmText = confirmText,
                    };

                    confirmationDialog.YesButtonClicked += YesClickedHandler;
                    confirmationDialog.NoButtonClicked += NoClickedHandler;
                    confirmationDialog.Closed += ClosedHandler;
                    confirmationDialog.Opened += confirmationDialog.OnOpened;

                    _ = confirmationDialog.ShowDialog( parentWindow );
                    return;

                    void CleanupHandlers()
                    {
                        confirmationDialog.YesButtonClicked -= YesClickedHandler;
                        confirmationDialog.NoButtonClicked -= NoClickedHandler;
                        confirmationDialog.Closed -= ClosedHandler;
                    }

                    void YesClickedHandler( object sender, RoutedEventArgs e )
                    {
                        CleanupHandlers();
                        confirmationDialog.Close();
                        tcs.SetResult( true );
                    }

                    void NoClickedHandler( object sender, RoutedEventArgs e )
                    {
                        CleanupHandlers();
                        confirmationDialog.Close();
                        tcs.SetResult( false );
                    }

                    void ClosedHandler( object sender, EventArgs e )
                    {
                        CleanupHandlers();
                        tcs.SetResult( null );
                    }
                }
            );

            return await tcs.Task;
        }

        public event EventHandler<RoutedEventArgs> YesButtonClicked
        {
            add => AddHandler( s_yesButtonClickedEvent, value );
            remove => RemoveHandler( s_yesButtonClickedEvent, value );
        }

        public event EventHandler<RoutedEventArgs> NoButtonClicked
        {
            add => AddHandler( s_noButtonClickedEvent, value );
            remove => RemoveHandler( s_noButtonClickedEvent, value );
        }

        private void YesButton_Click( [CanBeNull] object sender, [CanBeNull] RoutedEventArgs e ) =>
            RaiseEvent( new RoutedEventArgs( s_yesButtonClickedEvent ) );

        private void NoButton_Click( [CanBeNull] object sender, [CanBeNull] RoutedEventArgs e ) =>
            RaiseEvent( new RoutedEventArgs( s_noButtonClickedEvent ) );
    }
}
