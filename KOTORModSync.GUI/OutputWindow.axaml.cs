// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using KOTORModSync.Core;
using Path = System.IO.Path;

namespace KOTORModSync
{
    public class OutputViewModel : INotifyPropertyChanged
    {
        public Queue<string> _logBuilder = new Queue<string>();
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
        protected virtual void OnPropertyChanged(string propertyName)
        {
            LogText = string.Join( Environment.NewLine, _logBuilder );
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
    public partial class OutputWindow : Window
    {
        public OutputViewModel _viewModel;
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
            Logger.Logged += message => AppendLogAsync(message);

            Logger.ExceptionLogged += ex => 
            {
                string exceptionLog = $"Exception: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}";
                AppendLogAsync(exceptionLog);
            };

            string logfileName = $"{Logger.LogFileName}{DateTime.Now:yyyy-MM-dd}";
            string executingDirectory = Core.Utility.Utility.GetExecutingAssemblyDirectory();
            string filePath = Path.Combine(executingDirectory, logfileName + ".txt");
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                int startIndex = Math.Max(0, lines.Length - _maxLinesShown);
                foreach (string line in lines.Skip(startIndex))
                {
                    AppendLogAsync(line);
                }
                LogScrollViewer.ScrollToEnd();
            }
        }

        private object _logLock = new object();
        private async Task AppendLogAsync(string message)
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

                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        // Scroll to the end of the content
                        LogScrollViewer.ScrollToEnd();
                    }
                );
            }
            catch ( Exception ex )
            {
            }
        }
    }
}
