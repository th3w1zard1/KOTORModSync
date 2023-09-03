// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{
	public class BooleanToArrowConverter : IValueConverter
	{
		public object Convert(
			[CanBeNull] object value,
			[NotNull] Type targetType,
			[CanBeNull] object parameter,
			[NotNull] CultureInfo culture
		) =>
			value is bool isExpanded && targetType == typeof( string )
				? isExpanded
					? "▼"
					: "▶"
				: (object)string.Empty;

		public object ConvertBack(
			[CanBeNull] object value,
			[NotNull] Type targetType,
			[CanBeNull] object parameter,
			[NotNull] CultureInfo culture
		) =>
			throw new NotSupportedException();
	}
}
