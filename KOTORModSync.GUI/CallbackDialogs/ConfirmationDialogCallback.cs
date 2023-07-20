// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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

        public Task<bool?> ShowConfirmationDialog( [CanBeNull] string message ) =>
            ConfirmationDialog.ShowConfirmationDialog( _topLevelWindow, message );
    }
}
