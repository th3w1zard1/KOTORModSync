// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Utility
{
    public class CallbackObjects
    {
        public static IConfirmationDialogCallback ConfirmCallback { get; private set; }
        public static IOptionsDialogCallback OptionsCallback { get; private set; }

        public static void SetCallbackObjects
        (
            IConfirmationDialogCallback confirmDialog,
            IOptionsDialogCallback optionsDialog
        )
        {
            ConfirmCallback = confirmDialog;
            OptionsCallback = optionsDialog;
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
