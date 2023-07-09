using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.CallbackDialogs
{
    internal sealed class ConfirmationDialogCallback : CallbackObjects.IConfirmationDialogCallback
    {
        private readonly Window _topLevelWindow;

        public ConfirmationDialogCallback( [CanBeNull] Window topLevelWindow ) => _topLevelWindow = topLevelWindow;

        public Task<bool?> ShowConfirmationDialog
            ( [CanBeNull] string message ) => ConfirmationDialog.ShowConfirmationDialog( _topLevelWindow, message );
    }
}
