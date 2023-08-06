// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Utilities;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public class ActionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value is null )
            {
                return Instruction.ActionType.Unset.ToString();
            }

            if ( value is Instruction.ActionType actionType )
            {
                return actionType.ToString();
            }

            if ( value is string strValue && Enum.TryParse( strValue, true, out Instruction.ActionType result ) )
                return result.ToString();
            
            string msg = $"Valid actions are [{string.Join( separator: ", ", Instruction.ActionTypes )}]";
            return new BindingNotification( new ArgumentException( msg ), BindingErrorType.Error );
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value is Instruction.ActionType actual )
                return actual.ToString();
            if ( value?.ToString() is string strValue )
            {
                if ( Enum.TryParse( strValue, true, out Instruction.ActionType result) )
                {
                    return result.ToString();
                }
            }

            return Instruction.ActionType.Unset.ToString();
        }
    }
}