// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;

namespace KOTORModSync.Controls
{
    public class TooltipProperties
    {
        public static readonly AttachedProperty<string> TooltipProperty =
            AvaloniaProperty.RegisterAttached<TooltipProperties, Control, string>( "Tooltip" );

        public static string GetTooltip( Control element ) => element.GetValue( TooltipProperty );

        public static void SetTooltip( Control element, string value ) => element.SetValue( TooltipProperty, value );
    }
}
