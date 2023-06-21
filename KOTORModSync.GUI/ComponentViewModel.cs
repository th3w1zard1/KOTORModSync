// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.ComponentModel;
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync
{
    public class ComponentViewModel : INotifyPropertyChanged
    {
        private readonly Component _component;
        private bool _isSelected;

        public ComponentViewModel( Component component ) => _component = component;

        public string Name => _component.Name;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if ( _isSelected == value )
                    return;

                _isSelected = value;
                OnPropertyChanged( nameof( IsSelected ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged
            ( string propertyName ) => PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
