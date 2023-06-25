﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data;
using Avalonia.Data.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class StringToGuidConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            // Convert Guid to string
            if ( value is Guid guid )
                return guid.ToString();

            return null;
        }

        public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
        {
            try
            {
                // Convert Guid to string
                string parsedGuid = Serializer.FixGuidString( (string)value );
                return Guid.Parse( parsedGuid ?? string.Empty );
            }
            catch ( FormatException e )
            {
                return new BindingNotification( new FormatException( e.Message ), BindingErrorType.DataValidationError );
            }
            catch ( Exception ex )
            {
                return new BindingNotification( new FormatException( ex.Message ), BindingErrorType.Error );
            }
        }
    }
}
