// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace KOTORModSync.GUI
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
