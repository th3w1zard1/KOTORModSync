// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace KOTORModSync.GUI.Converters
{
    public class ComboBoxItemConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) =>
            value is string action
                ? new ComboBoxItem { Content = action }
                : (object)null;

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) =>
            value is ComboBoxItem comboBoxItem
                ? comboBoxItem.Content?.ToString()
                : (object)null;
    }
}
