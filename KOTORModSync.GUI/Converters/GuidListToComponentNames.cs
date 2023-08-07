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
                foreach ( Component component in componentsList )
                {
                    if ( guids.Contains( component.Guid ) )
                        selectedComponentNames.Add( component.Name );
                }

                if ( selectedComponentNames.Count == 0 )
                {
                    // Both lists are empty or only selectedComponentNames is empty but guids is not
                    selectedComponentNames.Add(
                        guids.Count == 0
                            ? "None Selected"
                            : "Non-Existent Component in List"
                    );
                }

                // There's a mismatch in counts between selectedComponentNames and guids
                else if (selectedComponentNames.Count != guids.Count)
                {
                    selectedComponentNames.Add("Non-existent Component in List");
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
