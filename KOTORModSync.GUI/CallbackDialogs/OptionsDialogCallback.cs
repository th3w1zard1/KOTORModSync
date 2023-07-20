// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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

        [NotNull]
        public Task<string> ShowOptionsDialog( [CanBeNull] List<string> options ) =>
            OptionsDialog.ShowOptionsDialog( _topLevelWindow, options );
    }
}
