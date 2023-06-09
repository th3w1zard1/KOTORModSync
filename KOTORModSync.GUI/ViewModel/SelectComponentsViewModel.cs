﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace KOTORModSync.ViewModel
{
    public class SelectComponentsViewModel : INotifyPropertyChanged
    {
        public SelectComponentsViewModel()
        {
            // Initialize collections and commands
            Components = new ObservableCollection<ComponentViewModel>();
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            NextCommand = new RelayCommand( Next );
        }

        public ObservableCollection<ComponentViewModel> Components { get; set; }
        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    // RelayCommand class for ICommand implementation
}
