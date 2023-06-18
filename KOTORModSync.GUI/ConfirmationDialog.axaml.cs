// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
    internal sealed class ConfirmationDialogCallback : CallbackObjects.IConfirmationDialogCallback
    {
        private readonly Window _topLevelWindow;

        public ConfirmationDialogCallback( Window topLevelWindow ) => _topLevelWindow = topLevelWindow;

        public Task<bool?> ShowConfirmationDialog
            ( string message ) => ConfirmationDialog.ShowConfirmationDialog( _topLevelWindow, message );
    }

    public partial class ConfirmationDialog : Window
    {
        public static readonly AvaloniaProperty s_confirmTextProperty =
            AvaloniaProperty.Register<ConfirmationDialog, string>( nameof( ConfirmText ) );

        private static readonly RoutedEvent<RoutedEventArgs> s_yesButtonClickedEvent =
            RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
                nameof( YesButtonClicked ),
                RoutingStrategies.Bubble
            );

        private static readonly RoutedEvent<RoutedEventArgs> s_noButtonClickedEvent =
            RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
                nameof( NoButtonClicked ),
                RoutingStrategies.Bubble
            );

        public ConfirmationDialog() => InitializeComponent();

        public string ConfirmText
        {
            get => GetValue( s_confirmTextProperty ) as string;
            set => SetValue( s_confirmTextProperty, value );
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void OnOpened( object sender, EventArgs e )
        {
            var confirmTextBlock = this.FindControl<TextBlock>( "ConfirmTextBlock" );
            confirmTextBlock.Text = ConfirmText;
        }

        public static async Task<bool?> ShowConfirmationDialog( Window parentWindow, string confirmText )
        {
            var tcs = new TaskCompletionSource<bool?>();

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var confirmationDialog = new ConfirmationDialog { ConfirmText = confirmText };

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

        private void YesButton_Click
            ( object sender, RoutedEventArgs e ) => RaiseEvent( new RoutedEventArgs( s_yesButtonClickedEvent ) );

        private void NoButton_Click
            ( object sender, RoutedEventArgs e ) => RaiseEvent( new RoutedEventArgs( s_noButtonClickedEvent ) );
    }
}
