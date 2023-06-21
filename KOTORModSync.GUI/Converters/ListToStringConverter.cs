// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Data.Converters;

namespace KOTORModSync.GUI.Converters
{
    public class ListToStringConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is IEnumerable list ) )
            {
                return string.Empty;
            }

            var serializedList = new StringBuilder();
            foreach ( object item in list )
            {
                if ( item == null )
                {
                    continue;
                }

                _ = serializedList.AppendLine( item.ToString() );
            }

            return serializedList.ToString();
        }


        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( !( value is string text ) )
            {
                return new List<string>();
            }

            string[] lines = text.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries );
            return targetType != typeof( List<Guid> )
                ? lines.ToList()
                : (object)lines.Select( line => Guid.TryParse( line, out Guid guid ) ? guid : Guid.Empty ).ToList();
        }
    }
}
