// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{
    public class ComboBoxItemConverter : IValueConverter
    {
        [CanBeNull]
        public object Convert(
            [CanBeNull] object value,
            [CanBeNull] Type targetType,
            [CanBeNull] object parameter,
            [CanBeNull] CultureInfo culture
        ) =>
            value is string action
                ? new ComboBoxItem { Content = action }
                : (object)null;

        [CanBeNull]
        public object ConvertBack(
            [CanBeNull] object value,
            [CanBeNull] Type targetType,
            [CanBeNull] object parameter,
            [CanBeNull] CultureInfo culture
        ) =>
            value is ComboBoxItem comboBoxItem
                ? comboBoxItem.Content?.ToString()
                : (object)null;
    }
}
