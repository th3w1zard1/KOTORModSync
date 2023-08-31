// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public class GuidListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
	        value is List<string> stringList
		        ? stringList.Select(s => Guid.Parse(Serializer.FixGuidString(s))).ToList()
		        : AvaloniaProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
	        value is List<Guid> guidList
		        ? guidList.Select(guid => guid.ToString()).ToList()
		        : AvaloniaProperty.UnsetValue;
    }
}
