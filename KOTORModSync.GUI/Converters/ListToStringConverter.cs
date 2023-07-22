// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Data;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class ListToStringConverter : IValueConverter
    {
        [NotNull]
        public static string RemoveSpacesExceptNewLine( [NotNull] string input )
        {
            if ( input is null )
            {
                throw new ArgumentNullException( nameof( input ) );
            }

            string pattern = $@"(?:(?!{Environment.NewLine})[^\S{Environment.NewLine}])+";
            string result = Regex.Replace( input, pattern, replacement: "" );

            return result;
        }

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            if ( !( value is IEnumerable list ) )
            {
                return string.Empty;
            }

            var serializedList = new StringBuilder();
            foreach ( object item in list )
            {
                if ( item is null )
                {
                    continue;
                }

                _ = serializedList.AppendLine( item.ToString() );
            }

            return serializedList.ToString();
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
                if ( !( value is string text ) )
                {
                    return null;
                }

                if ( targetType != typeof( List<Guid> ) )
                {
                    return text.Split( new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries )
                        .ToList();
                }

                string[] lines = RemoveSpacesExceptNewLine( text )
                    .Split( new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries );

                var guids = new List<Guid>();
                foreach ( string line in lines )
                {
                    try
                    {
                        guids.Add( Guid.Parse( Serializer.FixGuidString( line ) ) );
                    }
                    catch ( FormatException e )
                    {
                        return new BindingNotification(
                            new FormatException( e.Message ),
                            BindingErrorType.DataValidationError
                        );
                    }
                }

                return guids;
            }
            catch ( Exception ex )
            {
                return new BindingNotification( new FormatException( ex.Message ), BindingErrorType.Error );
            }
        }
    }
}
