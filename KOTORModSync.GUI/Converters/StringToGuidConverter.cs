// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class StringToGuidConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) =>
            // Convert Guid to string
            value is Guid guid
                ? guid.ToString()
                : (object)null;

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            try
            {
                // Convert Guid to string
                string parsedGuid = Serializer.FixGuidString( (string)value );
                return Guid.Parse( parsedGuid );
            }
            catch ( FormatException e )
            {
                return new BindingNotification(
                    new FormatException( e.Message ),
                    BindingErrorType.DataValidationError
                );
            }
            catch ( Exception ex )
            {
                return new BindingNotification( new FormatException( ex.Message ), BindingErrorType.Error );
            }
        }
    }
}
