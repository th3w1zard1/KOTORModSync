using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KOTORModSync.Installer.ViewModels;

namespace KOTORModSync.Installer.Views
{
    public partial class WelcomeScreenView : UserControl
    {
        public WelcomeScreenView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );
        }
    }
}
