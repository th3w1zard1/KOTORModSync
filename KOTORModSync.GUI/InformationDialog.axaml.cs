// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JetBrains.Annotations;

namespace KOTORModSync
{
	public partial class InformationDialog : Window
	{
		public static readonly AvaloniaProperty InfoTextProperty =
			AvaloniaProperty.Register<InformationDialog, string>("InfoText");

		public InformationDialog() => InitializeComponent();

		[CanBeNull]
		public string InfoText
		{
			get => GetValue(InfoTextProperty) as string;
			set => SetValue(InfoTextProperty, value);
		}

		public static async Task ShowInformationDialog(
			[NotNull] Window parentWindow,
			[CanBeNull] string message,
			[CanBeNull] string title = "Information"
		)
		{
			var dialog = new InformationDialog
			{
				Title = title, InfoText = message, Topmost = true,
			};
			_ = await dialog.ShowDialog<bool?>(parentWindow);
		}

		protected override void OnOpened([NotNull] EventArgs e)
		{
			base.OnOpened(e);
			UpdateInfoText();
		} // ReSharper disable twice UnusedParameter.Local
		private void OKButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => Close();
		private void UpdateInfoText() => Dispatcher.UIThread.InvokeAsync(() => InfoTextBlock.Text = InfoText);
	}
}
