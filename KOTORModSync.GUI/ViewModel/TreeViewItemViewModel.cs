// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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

namespace KOTORModSync.ViewModel
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
