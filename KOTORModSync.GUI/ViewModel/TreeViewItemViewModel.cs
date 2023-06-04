// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
using System.Threading.Tasks;

namespace KOTORModSync.ViewModel
After:
using System.Threading.Tasks;

namespace KOTORModSync.ViewModel
*/

namespace KOTORModSync.ViewModel
{
    public class TreeViewItemViewModel : INotifyPropertyChanged
    {
        private string _name;
        private bool _isSelected;
        private bool _isExpanded;
        private ObservableCollection<TreeViewItemViewModel> _childItems;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TreeViewItemViewModel> ChildItems
        {
            get => _childItems;
            set
            {
                _childItems = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}