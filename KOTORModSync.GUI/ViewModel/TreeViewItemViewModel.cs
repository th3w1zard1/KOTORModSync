// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KOTORModSync.GUI.ViewModel
{
    public class TreeViewItemViewModel : INotifyPropertyChanged
    {
        private string name;
        private bool isSelected;
        private bool isExpanded;
        private ObservableCollection<TreeViewItemViewModel> childItems;

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TreeViewItemViewModel> ChildItems
        {
            get { return childItems; }
            set
            {
                childItems = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
