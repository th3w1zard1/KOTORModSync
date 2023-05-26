// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
/* Unmerged change from project 'KOTORModSync (net6.0)'
Before:
namespace KOTORModSync.GUI
After:
namespace KOTORModSync;
using KOTORModSync;
using KOTORModSync.GUI
*/


namespace KOTORModSync
{
    public class AttachedProperties
    {
        public static readonly AttachedProperty<ICommand> ItemClickCommandProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, ICommand>("ItemClickCommand");


        /* Unmerged change from project 'KOTORModSync (net6.0)'
        Before:
                public static ICommand GetItemClickCommand(Control control)
                {
                    return control.GetValue(ItemClickCommandProperty);
        After:
                public static ICommand GetItemClickCommand(Control control) control.GetValue(ItemClickCommandProperty);
        */
        public static ICommand GetItemClickCommand(Control control) => => control.GetValue(ItemClickCommandProperty);

        public static void SetItemClickCommand(Control control, ICommand value) => control.SetValue(ItemClickCommandProperty, value);

        public static readonly AttachedProperty<object> ItemClickCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, object>("ItemClickCommandParameter");

        public static object GetItemClickCommandParameter(Control control) => control.GetValue(ItemClickCommandParameterProperty);

        public static void SetItemClickCommandParameter(Control control, object value) => control.SetValue(ItemClickCommandParameterProperty, value);
    }
}
