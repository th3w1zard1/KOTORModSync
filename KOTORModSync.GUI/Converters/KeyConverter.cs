﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
    public class KeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is KeyValuePair<string, string> keyValue)
            {
                return keyValue.Key;
            }
            return null;
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if(value is KeyValuePair<string, string> keyValue)
            {
                return keyValue.Key;
            }
            return null;
        }
    }

}
