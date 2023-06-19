// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.GUI.Converters
{
    public class EmptyCollectionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value is ICollection collection && collection.Count == 0 )
            {
                return new List<string> { string.Empty }; // Create a new collection with a default value
            }

            return value;
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) =>
            throw new NotSupportedException();
    }
}
