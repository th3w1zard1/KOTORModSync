using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.CallbackDialogs
{
    internal sealed class OptionsDialogCallback : CallbackObjects.IOptionsDialogCallback
    {
        private readonly Window _topLevelWindow;

        public OptionsDialogCallback( Window topLevelWindow ) => _topLevelWindow = topLevelWindow;

        public Task<string> ShowOptionsDialog
            ( List<string> options ) => OptionsDialog.ShowOptionsDialog( _topLevelWindow, options );
    }
}
