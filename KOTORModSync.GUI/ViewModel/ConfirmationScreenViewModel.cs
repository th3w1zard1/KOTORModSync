using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace KOTORModSync.ViewModel
{
    public class ConfirmationScreenViewModel : INotifyPropertyChanged
    {
        public ConfirmationScreenViewModel()
        {
            // Initialize collections and commands
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            BackCommand = new RelayCommand( Back );
            NextCommand = new RelayCommand( Next );
        }

        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand BackCommand { get; set; }
        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Back()
        {
            // Handle navigation to the previous screen
        }

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }
}
