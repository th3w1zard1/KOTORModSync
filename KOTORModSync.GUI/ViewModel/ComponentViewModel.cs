using System.ComponentModel;
using JetBrains.Annotations;

namespace KOTORModSync.ViewModel
{
    public class ComponentViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if ( _isSelected == value )
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged( nameof( IsSelected ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged
            ( [CanBeNull] string propertyName ) => PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
