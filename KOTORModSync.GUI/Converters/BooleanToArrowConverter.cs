// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.GUI.Converters
{
    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) =>
            value is bool isExpanded && targetType == typeof( string ) ? isExpanded ? "▼" : "▶" : (object)string.Empty;

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) =>
            throw new NotSupportedException();
    }
}
