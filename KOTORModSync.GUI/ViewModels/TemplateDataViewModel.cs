using System.ComponentModel;
using KOTORModSync.GUI_old.Models;

namespace KOTORModSync.GUI_old.ViewModels
{
    public class TemplateDataViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public TemplateData TemplateData { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public TemplateDataViewModel(TemplateData templateData)
        {
            TemplateData = templateData;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
