using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KOTORModSync.GUI_old.ViewModels;

namespace KOTORModSync.GUI_old
{
    public class MainWindow : Window
    {
        private ItemsControl itemsControl;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            itemsControl = this.Find<ItemsControl>("itemsControl");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            viewModel.SubmitSelectedTemplatesCommand.Execute(itemsControl.Items);
        }
    }
}
