using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.GUI
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(YesButtonClickedEvent));
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(NoButtonClickedEvent));
        }

        public static Task<bool> ShowConfirmationDialog(Window parentWindow)
        {
            var confirmationDialog = new ConfirmationDialog();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

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

            confirmationDialog.ShowDialog(parentWindow);

            return tcs.Task;
        }

    }
}
