using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.CallbackDialogs
{
    internal sealed class OptionsDialogCallback : CallbackObjects.IOptionsDialogCallback
    {
        private readonly Window _topLevelWindow;

        public OptionsDialogCallback( [CanBeNull] Window topLevelWindow ) => _topLevelWindow = topLevelWindow;

        public Task<string> ShowOptionsDialog
            ( [CanBeNull] List<string> options ) => OptionsDialog.ShowOptionsDialog( _topLevelWindow, options );
    }
}
