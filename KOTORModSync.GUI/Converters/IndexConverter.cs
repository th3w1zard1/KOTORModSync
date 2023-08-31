// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{
    public class IndexConverter : IMultiValueConverter
    {
	    public object Convert(
		    [NotNull] IList<object> values,
		    [NotNull] Type targetType,
		    [CanBeNull] object parameter,
		    [NotNull] CultureInfo culture
		)
	    {
		    return values[0] is IList list ? $"{list.IndexOf(values[1])+1}:" : "-1";
	    }
    }
}
