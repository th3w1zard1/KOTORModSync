// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.GUI
{
    internal class ConfirmationDialogCallback : Utility.IConfirmationDialogCallback
    {
        private readonly Window _topLevelWindow;

        public ConfirmationDialogCallback(Window topLevelWindow)
        {
            _topLevelWindow = topLevelWindow;
        }

        public Task<bool> ShowConfirmationDialog(string message)
        {
            return ConfirmationDialog.ShowConfirmationDialog(_topLevelWindow, message);
        }
    }



}
