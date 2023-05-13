// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KOTORModSync.GUI
{
    public class ComponentViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private ObservableCollection<ComponentViewModel> _childComponents = new ObservableCollection<ComponentViewModel>();

        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }

                // If this component is being deselected, deselect its child components as well
                if (!_isSelected)
                {
                    foreach (ComponentViewModel childComponent in ChildComponents)
                    {
                        childComponent.IsSelected = false;
                    }
                }
            }
        }

        public ObservableCollection<ComponentViewModel> ChildComponents
        {
            get => _childComponents;
            set
            {
                _childComponents = value;
                OnPropertyChanged(nameof(ChildComponents));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
