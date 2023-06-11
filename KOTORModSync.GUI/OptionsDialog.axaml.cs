using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
namespace KOTORModSync
{
    internal sealed class OptionsDialogCallback : Utility.IOptionsDialogCallback
    {
        private readonly Window _topLevelWindow;

        public OptionsDialogCallback(Window topLevelWindow)
        {
            _topLevelWindow = topLevelWindow;
        }

        public Task<string> ShowOptionsDialog(List<string> options)
        {
            return OptionsDialog.ShowOptionsDialog(_topLevelWindow, options);
        }
    }

    public partial class OptionsDialog : Window
    {
        public static readonly AvaloniaProperty OptionsListProperty =
            AvaloniaProperty.Register<OptionsDialog, List<string>>(nameof(OptionsList));

        [CanBeNull]
        public List<string> OptionsList
        {
            get => GetValue(OptionsListProperty) as List<string>;
            set => SetValue(OptionsListProperty, value);
        }

        public OptionsDialog()
        {
            InitializeComponent();
            optionTextBox = this.FindControl<TextBlock>("optionTextBox");
            optionStackPanel = this.FindControl<StackPanel>("optionStackPanel");
            okButton = this.FindControl<Button>("okButton");
            okButton.Click += OKButton_Click;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            RadioButton selectedRadioButton = optionStackPanel
                .Children
                .OfType<RadioButton>()
                .SingleOrDefault(rb => rb.IsChecked == true);

            if (selectedRadioButton != null)
            {
                string selectedOption = selectedRadioButton.Content?.ToString();
                OptionSelected?.Invoke(this, selectedOption);
            }

            Close();
        }

        public event EventHandler<string> OptionSelected;

        private void OnOpened(object sender, EventArgs e)
        {
            StackPanel optionStackPanel = this.FindControl<StackPanel>("optionStackPanel");

            foreach (string option in OptionsList)
            {
                var radioButton = new RadioButton
                {
                    Content = option,
                    GroupName = "OptionsGroup"
                };
                optionStackPanel.Children.Add(radioButton);
            }

            // Measure and arrange the optionStackPanel to update DesiredSize
            optionStackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            optionStackPanel.Arrange(new Rect(optionStackPanel.DesiredSize));

            // Get the actual size of the optionStackPanel including children and transformations
            Size actualSize = optionStackPanel.Bounds.Size;

            // Define padding values
            double horizontalPadding = 100; // Padding on the left and right
            double verticalPadding = 150;  // Padding on the top and bottom

            // Calculate the desired width and height for the content with padding
            double contentWidth = actualSize.Width   + (2 * horizontalPadding);
            double contentHeight = actualSize.Height + (2 * verticalPadding);

            // Set the width and height of the window
            this.Width = contentWidth;
            this.Height = contentHeight;

            this.InvalidateArrange();
            this.InvalidateMeasure();

            // Center the window on the screen
            Screen screen = Screens.ScreenFromVisual(this);
            double screenWidth = screen.Bounds.Width;
            double screenHeight = screen.Bounds.Height;
            double left = (screenWidth - contentWidth)  / 2;
            double top = (screenHeight - contentHeight) / 2;
            this.Position = new PixelPoint((int)left, (int)top);
        }

        public static async Task<string> ShowOptionsDialog(Window parentWindow, List<string> optionsList)
        {
            var tcs = new TaskCompletionSource<string>();

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var optionsDialog = new OptionsDialog { OptionsList = optionsList };

                optionsDialog.Closed += ClosedHandler;
                optionsDialog.Opened += optionsDialog.OnOpened;

                void ClosedHandler(object sender, EventArgs e)
                {
                    optionsDialog.Closed -= ClosedHandler;
                    tcs.TrySetResult(null);
                }

                optionsDialog.OptionSelected += (sender, option) =>
                {
                    tcs.TrySetResult(option);
                };

                await optionsDialog.ShowDialog(parentWindow);
            });

            return await tcs.Task;
        }
    }
}
