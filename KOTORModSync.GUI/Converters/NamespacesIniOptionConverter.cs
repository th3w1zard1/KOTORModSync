// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.TSLPatcher;

namespace KOTORModSync.Converters
{
    public class NamespacesIniOptionConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            try
            {
                if ( !( value is Instruction dataContextInstruction ) )
                    return null;

                Component parentComponent = dataContextInstruction.GetParentComponent();
                if ( parentComponent is null )
                    return null;

                foreach ( string archivePath in new ComponentValidation( parentComponent ).GetAllArchivesFromInstructions() )
                {
                    if ( archivePath is null )
                        continue;

                    Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive( archivePath );
                    if ( result is null || !result.Any() )
                        continue;

                    if ( !result.Values.Any() )
                        continue;

                    var optionNames = new List<string>();
                    foreach (KeyValuePair<string, Dictionary<string, string>> section in result)
                    {
                        if (section.Value?.TryGetValue("Name", out string name) ?? false)
                        {
                            optionNames.Add(name);
                        }
                    }
                    
                    return optionNames;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogException( ex );
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

}
