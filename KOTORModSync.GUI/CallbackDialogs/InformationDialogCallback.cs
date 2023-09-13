// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.CallbackDialogs
{
	internal sealed class InformationDialogCallback : CallbackObjects.IInformationDialogCallback
	{
		private readonly Window _topLevelWindow;
		public InformationDialogCallback([CanBeNull] Window topLevelWindow) => _topLevelWindow = topLevelWindow;

		public async Task ShowInformationDialog([CanBeNull] string message) =>
			await InformationDialog.ShowInformationDialog(_topLevelWindow, message);
	}
}
