// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{
    public class EmptyCollectionConverter : IValueConverter
    {
        [CanBeNull]
        public object Convert(
            [CanBeNull] object value,
            [CanBeNull] Type targetType,
            [CanBeNull] object parameter,
            [CanBeNull] CultureInfo culture
        )
        {
            if ( value is ICollection collection && collection.Count == 0 )
                return new List<string>
                {
                    string.Empty,
                }; // Create a new collection with a default value

            return value;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) =>
            throw new NotSupportedException();
    }
}
