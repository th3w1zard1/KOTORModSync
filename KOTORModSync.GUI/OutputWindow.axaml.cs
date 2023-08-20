// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemPathing;
using Path = System.IO.Path;

namespace KOTORModSync
{
    public sealed class OutputViewModel : INotifyPropertyChanged
    {
        public readonly Queue<string> _logBuilder = new Queue<string>();
        public string LogText { get; set; } = string.Empty;

        public void AppendLog(string message)
        {
            _logBuilder.Enqueue(message);
            OnPropertyChanged(nameof(LogText));
        }

        public void RemoveOldestLog()
        {
            _logBuilder.Dequeue();
            OnPropertyChanged(nameof(LogText));
        }

        // used for ui
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            LogText = string.Join( Environment.NewLine, _logBuilder );
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
    public partial class OutputWindow : Window
    {
        public readonly OutputViewModel _viewModel;
        private readonly int _maxLinesShown = 150;

        public OutputWindow()
        {
            InitializeComponent();
            _viewModel = new OutputViewModel();
            DataContext = _viewModel;
            InitializeControls();
        }

        private void InitializeControls()
        {
            Logger.Logged += AppendLog;

            Logger.ExceptionLogged += ex => 
            {
                string exceptionLog = $"Exception: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}";
                AppendLog( exceptionLog );
            };

            string logfileName = $"{Logger.LogFileName}{DateTime.Now:yyyy-MM-dd}";
            string executingDirectory = Core.Utility.Utility.GetExecutingAssemblyDirectory();
            var logFilePath = new InsensitivePath(Path.Combine(executingDirectory, logfileName + ".txt"), isFile:true);
            if ( !logFilePath.Exists )
                return;

            string[] lines = File.ReadAllLines(logFilePath.FullName);
            int startIndex = Math.Max(0, lines.Length - _maxLinesShown);
            foreach (string line in lines.Skip(startIndex))
            {
                AppendLog( line );
            }
            LogScrollViewer.ScrollToEnd();
        }

        private readonly object _logLock = new object();
        private void AppendLog(string message)
        {
            try
            {
                lock ( _logLock )
                {
                    if ( _viewModel._logBuilder.Count >= _maxLinesShown )
                    {
                        _viewModel.RemoveOldestLog();
                    }

                    _viewModel.AppendLog( message );
                }

                // Scroll to the end of the content
                _ = Dispatcher.UIThread.InvokeAsync( () => LogScrollViewer.ScrollToEnd() );
            }
            catch ( Exception ex)
            {
                Console.WriteLine($"An error occurred appending the log to the output window: '{ex.Message}'");
            }
        }
    }
}
