// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
using System.Threading.Tasks;

namespace KOTORModSync.ViewModel
After:
using System.Threading.Tasks;

namespace KOTORModSync.ViewModel
*/

namespace KOTORModSync.GUI.ViewModel
{
    public class TreeViewItemViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<TreeViewItemViewModel> _childItems;
        private bool _isExpanded;
        private bool _isSelected;
        private string _name;

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

        protected virtual void OnPropertyChanged
            ( [CallerMemberName] [CanBeNull] string propertyName = null ) => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs( propertyName )
        );
    }
}
