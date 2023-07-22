// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class CallbackObjects
    {
        public static IConfirmationDialogCallback ConfirmCallback { get; private set; }
        public static IOptionsDialogCallback OptionsCallback { get; private set; }

        public static void SetCallbackObjects(
            [NotNull] IConfirmationDialogCallback confirmDialog,
            [NotNull] IOptionsDialogCallback optionsDialog
        )
        {
            ConfirmCallback = confirmDialog ?? throw new ArgumentNullException( nameof( confirmDialog ) );
            OptionsCallback = optionsDialog ?? throw new ArgumentNullException( nameof( optionsDialog ) );
        }

        public interface IConfirmationDialogCallback
        {
            Task<bool?> ShowConfirmationDialog( string message );
        }

        public interface IOptionsDialogCallback
        {
            Task<string> ShowOptionsDialog( List<string> options );
        }
    }
}
