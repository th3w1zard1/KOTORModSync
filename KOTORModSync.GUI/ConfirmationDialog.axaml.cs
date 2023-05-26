// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync.GUI
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog() => InitializeComponent();

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public static readonly AvaloniaProperty ConfirmTextProperty =
            AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(ConfirmText));

        public string ConfirmText
        {
            get => GetValue(ConfirmTextProperty) as string;
            set => SetValue(ConfirmTextProperty, value);
        }

        private void OnOpened(object sender, EventArgs e)
        {
            TextBlock confirmTextBlock = this.FindControl<TextBlock>("confirmTextBlock");
            confirmTextBlock.Text = ConfirmText;
        }

        public static Task<bool> ShowConfirmationDialog(Window parentWindow, string confirmText)
        {
            var tcs = new TaskCompletionSource<bool>();

            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                var confirmationDialog = new ConfirmationDialog
                {
                    ConfirmText = confirmText
                };

                EventHandler<RoutedEventArgs> yesClickedHandler = null;
                EventHandler<RoutedEventArgs> noClickedHandler = null;
                EventHandler closedHandler = null;

                yesClickedHandler = (sender, e) =>
                {
                    confirmationDialog.YesButtonClicked -= yesClickedHandler;
                    confirmationDialog.NoButtonClicked -= noClickedHandler;
                    confirmationDialog.Closed -= closedHandler;

                    confirmationDialog.Close();
                    tcs.SetResult(true);
                };

                noClickedHandler = (sender, e) =>
                {
                    confirmationDialog.YesButtonClicked -= yesClickedHandler;
                    confirmationDialog.NoButtonClicked -= noClickedHandler;
                    confirmationDialog.Closed -= closedHandler;

                    confirmationDialog.Close();
                    tcs.SetResult(false);
                };

                closedHandler = (sender, e) =>
                {
                    confirmationDialog.YesButtonClicked -= yesClickedHandler;
                    confirmationDialog.NoButtonClicked -= noClickedHandler;
                    confirmationDialog.Closed -= closedHandler;

                    tcs.SetResult(false);
                };

                confirmationDialog.YesButtonClicked += yesClickedHandler;
                confirmationDialog.NoButtonClicked += noClickedHandler;
                confirmationDialog.Closed += closedHandler;
                confirmationDialog.Opened += confirmationDialog.OnOpened;

                _ = confirmationDialog.ShowDialog(parentWindow);
            });

            return tcs.Task;
        }

        public static readonly RoutedEvent<RoutedEventArgs> YesButtonClickedEvent =
            RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(nameof(YesButtonClicked), RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> YesButtonClicked
        {
            add => AddHandler(YesButtonClickedEvent, value);
            remove => RemoveHandler(YesButtonClickedEvent, value);
        }

        public static readonly RoutedEvent<RoutedEventArgs> NoButtonClickedEvent =
            RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(nameof(NoButtonClicked), RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> NoButtonClicked
        {
            add => AddHandler(NoButtonClickedEvent, value);
            remove => RemoveHandler(NoButtonClickedEvent, value);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(YesButtonClickedEvent));

        private void NoButton_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NoButtonClickedEvent));
    }
}
