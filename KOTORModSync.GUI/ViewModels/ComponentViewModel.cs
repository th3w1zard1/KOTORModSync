// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace KOTORModSync.GUI.ViewModels
{
    public class ComponentViewModel : ViewModelBase
    {
        private readonly Component _component;

        public ComponentViewModel(Component component)
        {
            _component = component;
        }

        public string Name => _component.Name;

        public bool IsChecked
        {
            get => _component.IsChecked;
            set
            {
                if (_component.IsChecked != value)
                {
                    _component.IsChecked = value;

                    CheckDependencies(_component, value);

                    RaisePropertyChanged(nameof(IsChecked));
                }
            }
        }
    }
}
