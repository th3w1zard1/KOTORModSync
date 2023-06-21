// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.GUI.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is IEnumerable )
                || !( parameter is TextBlock textBlock )
                || !( textBlock.Tag is ItemsRepeater itemsRepeater ) )
            {
                return string.Empty;
            }

            int index = -1;
            if ( itemsRepeater.Tag is IEnumerable itemList )
            {
                index = itemList.Cast<object>().ToList().IndexOf( value );
            }

            return index.ToString();
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is string indexString ) || !( parameter is ItemsRepeater itemsRepeater ) )
            {
                return null;
            }

            if ( !int.TryParse( indexString, out int index ) || !( itemsRepeater.Tag is IEnumerable itemList ) )
            {
                return null;
            }

            IEnumerable enumerable = itemList.Cast<object>().ToList();
            var itemList2 = enumerable.Cast<object>().ToList();
            return index < 0 || index >= itemList2.Count ? null : itemList2[index];
        }
    }
}
