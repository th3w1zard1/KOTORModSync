// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;

namespace KOTORModSync
{
	public partial class ProgressWindow : Window
	{
		public ProgressWindow() => InitializeComponent();
		public void Dispose() => Close();

		public static async Task ShowProgressWindow(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string message,
			decimal progress
		)
		{
			var progressWindow = new ProgressWindow
			{
				Owner = parentWindow,
				ProgressTextBlock =
				{
					Text = message,
				},
				ProgressBar =
				{
					Value = (double)progress,
				},
				Topmost = true,
			};

			if ( !(parentWindow is null) )
				_ = await progressWindow.ShowDialog<bool?>(parentWindow);
		}
	}
}
