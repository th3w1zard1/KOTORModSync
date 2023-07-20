// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KOTORModSync.Core;

namespace KOTORModSync
{
    public partial class OutputWindow : Window
    {
        private StringBuilder _logBuilder;
        private readonly int _maxLinesShown = 1000;

        public OutputWindow()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void InitializeControls()
        {
            LogTextBox = this.FindControl<TextBox>( "LogTextBox" );
            LogScrollViewer = this.FindControl<ScrollViewer>( "LogScrollViewer" );

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

            // Open the file and retrieve the last 200 lines
            string logfileName = $"{Logger.LogFileName}{DateTime.Now:yyyy-MM-dd}";
            string executingDirectory = Core.Utility.Utility.GetExecutingAssemblyDirectory();
            string filePath = Path.Combine( executingDirectory, logfileName + ".txt" );
            if ( !File.Exists( filePath ) )
                return;

            string[] lines = File.ReadAllLines( filePath );
            int startIndex = Math.Max( val1: 0, lines.Length - _maxLinesShown );
            string recentLines = string.Join( Environment.NewLine, lines, startIndex, lines.Length - startIndex );

            _ = _logBuilder.AppendLine( recentLines );
            UpdateLogText();
            LogScrollViewer.ScrollToEnd();
        }

        private void UpdateLogText()
        {
            lock ( _logBuilder )
            {
                // Create a local copy of _logBuilder to avoid accessing it from multiple threads
                string logText = _logBuilder.ToString();

                // Split the log text into lines
                string[] lines = logText.Split( new[] { Environment.NewLine }, StringSplitOptions.None );

                // Trim the lines if they exceed the desired line count
                if ( lines.Length > _maxLinesShown )
                {
                    lines = lines.Skip( lines.Length - _maxLinesShown ).ToArray();
                    logText = string.Join( Environment.NewLine, lines );
                }

                _ = Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        LogTextBox.Text = logText;

                        // Scroll to the end of the content
                        LogScrollViewer.ScrollToEnd();
                    }
                );
            }
        }
    }
}
