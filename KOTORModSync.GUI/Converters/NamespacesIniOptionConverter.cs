// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public class NamespacesIniOptionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) => ( value as Component )?.Name;
        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => value as List<Component> ?? new object();
    }
}
