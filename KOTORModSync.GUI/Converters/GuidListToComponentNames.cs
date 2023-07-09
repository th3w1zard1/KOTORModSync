// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public class GuidListToComponentNames : IMultiValueConverter
    {
        public object Convert
        (
            IList<object> values,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            try
            {
                if ( !( values[0] is List<Guid> guids )
                    || !( values[1] is List<Component> componentsList ) )
                {
                    return null;
                }

                var selectedComponentNames = new List<string>();
                foreach ( Component component in componentsList )
                {
                    if ( guids.Contains( component.Guid ) )
                    {
                        selectedComponentNames.Add( component.Name );
                    }
                }

                if ( selectedComponentNames.Count == 0 )
                {
                    return new List<string> { "None Selected" };
                }

                return selectedComponentNames;
            }
            catch ( Exception e )
            {
                Logger.LogException( e );
                return null;
            }
        }
    }
}
