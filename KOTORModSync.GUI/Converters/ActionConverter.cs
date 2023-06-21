// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Utilities;
using JetBrains.Annotations;

namespace KOTORModSync.GUI.Converters
{
    public class ActionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value == null )
            {
                return targetType.IsValueType ? AvaloniaProperty.UnsetValue : null;
            }

            if ( TypeUtilities.TryConvert( targetType, value, culture, out object result ) )
            {
                var validActions = new List<string>
                {
                    "move",
                    "delduplicate",
                    "copy",
                    "rename",
                    "tslpatcher",
                    "execute",
                    "run",
                    "delete",
                    "choose",
                    "extract"
                };

                if ( validActions.Any( action => string.Equals( action, (string)result, StringComparison.OrdinalIgnoreCase ) ) )
                {
                    return result;
                }

                string msg = $"Valid actions are [{string.Join( ", ", validActions )}]";
                return new BindingNotification( new ArgumentException( msg ), BindingErrorType.Error );
            }

            string message = TypeUtilities.IsNumeric( targetType )
                ? $"'{value}' is not a valid number."
                : $"Could not convert '{value}' to '{targetType.Name}'.";
            return new BindingNotification( new InvalidCastException( message ), BindingErrorType.Error );
        }

        public object ConvertBack
            ( object value, Type targetType, object parameter, CultureInfo culture ) =>
            Convert( value, targetType, parameter, culture );
    }
}
