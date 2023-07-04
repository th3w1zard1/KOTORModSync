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
    public class ComponentToGuidConverter : IValueConverter
    {
        [CanBeNull]
        public object Convert(
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
        )
        {
            if ( !( value is Component selectedComponent ) )
                return null;

            return selectedComponent.Name;
        }

        [CanBeNull]
        public object ConvertBack(
            [CanBeNull] object value,
            [NotNull] Type targetType,
            [CanBeNull] object parameter,
            [NotNull] CultureInfo culture
        )
        {
            if ( !( value is List<Component> ) )
                return null;

            return new object();
        }
    }

}
