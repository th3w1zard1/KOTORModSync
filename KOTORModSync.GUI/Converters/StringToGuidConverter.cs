// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class StringToGuidConverter : IValueConverter
    {
        [CanBeNull]
        public object Convert
        (
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
        )
        {
            // Convert Guid to string
            if ( value is Guid guid )
            {
                return guid.ToString();
            }

            return null;
        }

        public object ConvertBack
        (
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
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
