// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;

namespace KOTORModSync.GUI.Converters
{
    // from https://code.4noobz.net/wpf-enum-binding-with-description-in-a-combobox/
    public class EnumDescriptionConverter : IValueConverter
    {
        private static string GetEnumDescription( Enum enumObj )
        {
            FieldInfo fieldInfo = enumObj.GetType().GetField( enumObj.ToString() );
            object[] attribArray = fieldInfo.GetCustomAttributes( false );

            if ( attribArray.Length == 0 )
                return enumObj.ToString();
            else
            {
                DescriptionAttribute attrib = null;

                foreach ( object att in attribArray )
                {
                    if ( att is DescriptionAttribute attribute )
                        attrib = attribute;
                }

                if ( attrib != null )
                    return attrib.Description;

                return enumObj.ToString();
            }
        }

        object IValueConverter.Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            var myEnum = (Enum)value;
            string description = GetEnumDescription( myEnum );
            return description;
        }

        object IValueConverter.ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            return string.Empty;
        }
    }
}
