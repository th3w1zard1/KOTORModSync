// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        )
        {
            // Convert Guid to string
            return value is Guid guid ? guid.ToString() : (object)null;
        }

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
