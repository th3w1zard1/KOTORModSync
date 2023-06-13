using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Installer.Views
{
    public partial class InstallationProgressView : UserControl
    {
        public InstallationProgressView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );
        }
    }
}
