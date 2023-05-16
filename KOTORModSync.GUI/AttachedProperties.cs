using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KOTORModSync.GUI
{
    public class AttachedProperties
    {
        public static readonly AttachedProperty<ICommand> ItemClickCommandProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, ICommand>("ItemClickCommand");

        public static ICommand GetItemClickCommand(Control control)
        {
            return control.GetValue(ItemClickCommandProperty);
        }

        public static void SetItemClickCommand(Control control, ICommand value)
        {
            control.SetValue(ItemClickCommandProperty, value);
        }

        public static readonly AttachedProperty<object> ItemClickCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<AttachedProperties, Control, object>("ItemClickCommandParameter");

        public static object GetItemClickCommandParameter(Control control)
        {
            return control.GetValue(ItemClickCommandParameterProperty);
        }

        public static void SetItemClickCommandParameter(Control control, object value)
        {
            control.SetValue(ItemClickCommandParameterProperty, value);
        }
    }
}
