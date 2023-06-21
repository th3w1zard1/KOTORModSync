using System.ComponentModel;
using System.Windows.Input;

namespace KOTORModSync.ViewModel
{
    public class ResultsScreenViewModel : INotifyPropertyChanged
    {
        public ResultsScreenViewModel() =>
            // Initialize commands
            FinishCommand = new RelayCommand( Finish );

        public ICommand FinishCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Finish()
        {
            // Handle finalization of the installation process
        }
    }
}