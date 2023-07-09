// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync
{
    public partial class OutputWindow : Window
    {
        private StringBuilder _logBuilder;
        private ScrollViewer _logScrollViewer;
        private TextBox _logTextBox;

        public OutputWindow()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void InitializeControls()
        {
            _logTextBox = this.FindControl<TextBox>( "LogTextBox" );
            _logScrollViewer = this.FindControl<ScrollViewer>( "LogScrollViewer" );

            _logBuilder = new StringBuilder( 65535 );

            // Subscribe to the Logger.Logged event to capture log messages
            Logger.Logged += message =>
            {
                _ = _logBuilder.AppendLine( message );
                UpdateLogText();
            };

            // Subscribe to the Logger.ExceptionLogged event to capture exceptions
            Logger.ExceptionLogged += ex =>
            {
                _ = _logBuilder.Append( "Exception: " )
                    .Append( ex.GetType().Name )
                    .Append( ": " )
                    .AppendLine( ex.Message )
                    .Append( "Stack trace: " )
                    .AppendLine( ex.StackTrace );
                UpdateLogText();
            };
        }

        private void UpdateLogText()
        {
            lock ( _logBuilder )
            {
                // Create a local copy of _logBuilder to avoid accessing it from multiple threads
                string logText = _logBuilder.ToString();

                _ = Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        _logTextBox.Text = logText;

                        // Scroll to the end of the content
                        _logScrollViewer?.ScrollToEnd();
                    }
                );
            }
        }

        private void logTextBox_TextChanged( [CanBeNull] object sender, [CanBeNull] AvaloniaPropertyChangedEventArgs e ) => UpdateLogText();
    }
}
