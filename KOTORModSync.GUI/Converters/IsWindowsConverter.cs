using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Data.Converters;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	public class IsWindowsConverter : IValueConverter
    {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Utility.GetOperatingSystem() == OSPlatform.Windows;
		}

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}