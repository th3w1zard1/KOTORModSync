// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using JetBrains.Annotations;

namespace KOTORModSync
{
    public class AttachedProperties
    {
        public static readonly AttachedProperty<ICommand> ItemClickCommandProperty
            = AvaloniaProperty.RegisterAttached<AttachedProperties, Control, ICommand>( "ItemClickCommand" );

        public static readonly AttachedProperty<object> ItemClickCommandParameterProperty
            = AvaloniaProperty.RegisterAttached<AttachedProperties, Control, object>( "ItemClickCommandParameter" );

        [CanBeNull]
        public static ICommand GetItemClickCommand( Control control ) => control.GetValue( ItemClickCommandProperty );

        public static void SetItemClickCommand
            ( Control control, [CanBeNull] ICommand value ) => control.SetValue( ItemClickCommandProperty, value );

        [CanBeNull]
        public static object GetItemClickCommandParameter
            ( Control control ) => control.GetValue( ItemClickCommandParameterProperty );

        public static void SetItemClickCommandParameter
            ( Control control, [CanBeNull] object value ) => control.SetValue( ItemClickCommandParameterProperty, value );
    }
}
