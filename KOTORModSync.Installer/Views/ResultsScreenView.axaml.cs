using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Installer.Views
{
    public partial class ResultsScreenView : UserControl
    {
        public ResultsScreenView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
