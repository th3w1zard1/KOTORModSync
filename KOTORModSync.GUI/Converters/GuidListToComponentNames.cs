// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public class GuidListToComponentNames : IMultiValueConverter
    {
        public object Convert(
            IList<object> values,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            try
            {
                if ( !( values[0] is List<Guid> guids ) || !( values[1] is List<Component> componentsList ) )
                    return null;

                var selectedComponentNames = new List<string>();
                foreach ( Guid cGuid in guids )
                {
                    Component foundComponent = Component.FindComponentFromGuid( cGuid, componentsList );
                    if ( !( foundComponent is null ) )
                        selectedComponentNames.Add( foundComponent.Name );
                    else
                        selectedComponentNames.Add( cGuid.ToString() );
                }

                if ( selectedComponentNames.Count == 0 )
                    selectedComponentNames.Add( "None" );

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
