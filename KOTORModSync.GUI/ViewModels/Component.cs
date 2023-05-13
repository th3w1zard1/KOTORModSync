// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;

namespace KOTORModSync.GUI.ViewModels
{
    public class Component : INotifyPropertyChanged
    {
        private bool _isChecked;
        private List<Component> _dependencies;

        public string Name { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    UpdateDependencies();
                }
            }
        }

        public List<Component> Dependencies
        {
            get => _dependencies;
            set
            {
                _dependencies = value;
                UpdateDependencies();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateDependencies()
        {
            if (!IsChecked || Dependencies == null)
            {
                return;
            }

            foreach (var dependency in Dependencies)
            {
                dependency.IsChecked = true;
            }
        }
    }
}
