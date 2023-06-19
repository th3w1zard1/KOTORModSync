// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
using System.Linq;
After:
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT System.Linq;
*/
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
            // Initialize the logger
            Logger.Initialize();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void InitializeControls()
        {
            _logTextBox = this.FindControl<TextBox>( "logTextBox" );
            _logScrollViewer = this.FindControl<ScrollViewer>( "logScrollViewer" );

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
                _ = _logBuilder
                    .Append( "Exception: " ).Append( ex.GetType().Name ).Append( ": " )
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

        private void logTextBox_TextChanged( object sender, AvaloniaPropertyChangedEventArgs e ) => UpdateLogText();
    }
}
