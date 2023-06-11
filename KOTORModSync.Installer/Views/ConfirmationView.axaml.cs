using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using KOTORModSync.Installer.ViewModels;

namespace KOTORModSync.Installer.Views
{
    public partial class ConfirmationView : ReactiveUserControl<ConfirmationViewModel>
    {
        public ConfirmationView()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
