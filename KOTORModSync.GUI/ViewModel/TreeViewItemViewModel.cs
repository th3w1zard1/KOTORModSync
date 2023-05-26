// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
using System.Threading.Tasks;

namespace KOTORModSync.GUI.ViewModel
After:
using System.Threading.Tasks;

namespace KOTORModSync.GUI.ViewModel
*/

namespace KOTORModSync.ViewModel
{
    public class TreeViewItemViewModel : INotifyPropertyChanged
    {
        private string name;
        private bool isSelected;
        private bool isExpanded;
        private ObservableCollection<TreeViewItemViewModel> childItems;

        public string Name
        {

            /* Unmerged change from project 'KOTORModSync (net6.0)'
            Before:
                        get { return name; }
            After:
                        get => name; }
            */
            get => name;
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {

            /* Unmerged change from project 'KOTORModSync (net6.0)'
            Before:
                        get { return isExpanded; }
            After:
                        get => isExpanded; }
            */
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TreeViewItemViewModel> ChildItems
        {

            /* Unmerged change from project 'KOTORModSync (net6.0)'
            Before:
                        get { return childItems; }
            After:
                        get => childItems; }
            */
            get => childItems;
            set
            {
                childItems = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
