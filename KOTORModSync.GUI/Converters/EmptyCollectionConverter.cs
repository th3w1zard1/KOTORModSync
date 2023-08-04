// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class EmptyCollectionConverter : IValueConverter
    {
        [CanBeNull]
        public object Convert(
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
        )
        {
            if ( value is ICollection collection && collection.IsNullOrEmptyCollection() )
                return new List<string>
                {
                    string.Empty,
                }; // Create a new collection with a default value

            return value;
        }

        public object ConvertBack(
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
        ) =>
            throw new NotSupportedException();
    }
}
