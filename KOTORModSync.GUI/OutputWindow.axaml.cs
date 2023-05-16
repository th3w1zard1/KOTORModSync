using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync.Core;

namespace KOTORModSync.GUI
{
    public partial class OutputWindow : Window
    {
        private TextBox _logTextBox;
        private ScrollViewer _logScrollViewer;
        private StringBuilder _logBuilder;

        public OutputWindow()
        {
            InitializeComponent();
            InitializeControls();
            // Initialize the logger
            Logger.Initialize();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _logTextBox = this.FindControl<TextBox>("logTextBox");
            _logScrollViewer = this.FindControl<ScrollViewer>("logScrollViewer");

            _logBuilder = new StringBuilder();

            // Subscribe to the Logger.Logged event to capture log messages
            Logger.Logged += (message) =>
            {
                _logBuilder.AppendLine(message);
                UpdateLogText();
            };

            // Subscribe to the Logger.ExceptionLogged event to capture exceptions
            Logger.ExceptionLogged += (ex) =>
            {
                _logBuilder.AppendLine($"Exception: {ex.GetType().Name} - {ex.Message}");
                _logBuilder.AppendLine($"Stack trace: {ex.StackTrace}");
                UpdateLogText();
            };
        }

        private void UpdateLogText()
        {
            lock (_logBuilder)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _logTextBox.Text = _logBuilder.ToString();

                    if (_logScrollViewer != null)
                    {
                        // Scroll to the end of the content
                        _logScrollViewer.ScrollToEnd();
                    }
                });
            }
        }
        private void logTextBox_TextChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            UpdateLogText();
        }
    }
}
