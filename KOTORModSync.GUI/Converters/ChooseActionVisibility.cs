// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public class ChooseActionVisibility : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is Instruction.ActionType action && action.Equals( Instruction.ActionType.Choose );
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}
