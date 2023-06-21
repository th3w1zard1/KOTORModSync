using System.ComponentModel;
using System.Windows.Input;

namespace KOTORModSync.ViewModel
{
    public class InstallationProgressScreenViewModel : INotifyPropertyChanged
    {
        public InstallationProgressScreenViewModel() =>
            // Initialize commands
            NextCommand = new RelayCommand( Next );

        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }
}
