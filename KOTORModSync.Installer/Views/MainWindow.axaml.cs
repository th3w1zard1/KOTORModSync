using Avalonia.Controls;
using Prism.Ioc;

namespace KOTORModSync.Installer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(null)
        {
        }

        public MainWindow(IContainerProvider? containerProvider)
        {
            InitializeComponent();
            if (containerProvider != null)
            {
                // Initialization logic using the containerProvider
            }
        }
    }
}