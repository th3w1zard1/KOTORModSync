using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public List<string> OptionsList
        {
            get => GetValue(OptionsListProperty) as List<string>;
            set => SetValue(OptionsListProperty, value);
        }

        public OptionsDialog()
        {
            InitializeComponent();
            optionsItemsControl = this.FindControl<ItemsControl>("optionsItemsControl");
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
            string selectedOption = null;
            foreach (var radioButton in this.optionStackPanel.Children.OfType<RadioButton>())
            {
                if (!radioButton.IsChecked != true) { continue; }

                selectedOption = radioButton.Content?.ToString();
                break;
            }

            if (selectedOption != null)
            {
                OptionSelected?.Invoke(this, selectedOption);
            }

            Close();
        }

        public event EventHandler<string> OptionSelected;


        private void OnOpened(object sender, EventArgs e)
        {
            var optionStackPanel = this.FindControl<StackPanel>("optionStackPanel");
            foreach (var option in OptionsList)
            {
                var radioButton = new RadioButton { Content = option, GroupName = "OptionsGroup" };
                optionStackPanel.Children.Add(radioButton);
            }
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
