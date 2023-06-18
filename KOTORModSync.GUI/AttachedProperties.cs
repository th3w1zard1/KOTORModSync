// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace KOTORModSync
{
    public class AttachedProperties
    {
        public static readonly AttachedProperty<ICommand> ItemClickCommandProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, ICommand>( "ItemClickCommand" );

        public static readonly AttachedProperty<object> ItemClickCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, object>( "ItemClickCommandParameter" );

        public static ICommand GetItemClickCommand( Control control ) => control.GetValue( ItemClickCommandProperty );

        public static void SetItemClickCommand
            ( Control control, ICommand value ) => control.SetValue( ItemClickCommandProperty, value );

        public static object GetItemClickCommandParameter
            ( Control control ) => control.GetValue( ItemClickCommandParameterProperty );

        public static void SetItemClickCommandParameter
            ( Control control, object value ) => control.SetValue( ItemClickCommandParameterProperty, value );
    }
}
