// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace KOTORModSync.GUI.Controls
{
    public class TooltipProperties
    {
        public static readonly AttachedProperty<string> TooltipProperty =
            AvaloniaProperty.RegisterAttached<TooltipProperties, Control, string>( "Tooltip" );

        public static string GetTooltip( Control element )
        {
            return element.GetValue( TooltipProperty );
        }

        public static void SetTooltip( Control element, string value )
        {
            element.SetValue( TooltipProperty, value );
        }
    }

}
