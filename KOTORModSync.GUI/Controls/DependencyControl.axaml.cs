using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
/* Unmerged change from project 'KOTORModSync (net462)'
Before:
using System;
After:
using Avalonia;
*/
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
    public partial class DependencyControl : UserControl
    {
        public DependencyControl() => InitializeComponent();

        // used to fix the move window code with comboboxes.
        protected override void OnAttachedToVisualTree( VisualTreeAttachmentEventArgs e )
        {
            base.OnAttachedToVisualTree( e );

            if ( VisualRoot is MainWindow mainWindow )
            {
                mainWindow.FindComboBoxesInWindow( mainWindow );
            }
        }

        [NotNull]
        public static readonly StyledProperty<List<Guid>> ThisGuidListProperty
            = AvaloniaProperty.Register<DependencyControl, List<Guid>>( nameof(ThisGuidList) );

        [NotNull]
        public List<Guid> ThisGuidList
        {
            get => GetValue( ThisGuidListProperty ) ?? throw new InvalidOperationException();
            set => SetValue( ThisGuidListProperty, value );
        }

        [NotNull]
        public static readonly StyledProperty<List<Component>> ThisComponentListProperty
            = AvaloniaProperty.Register<DependencyControl, List<Component>>( nameof( ThisComponentList ) );

        [NotNull]
        public List<Component> ThisComponentList
        {
            get => GetValue( ThisComponentListProperty ) ?? throw new InvalidOperationException();
            set => SetValue( ThisComponentListProperty, value );
        }

        private void AddToList_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( !( sender is Button addButton ) )
                {
                    throw new ArgumentException( "Sender is not a Button." );
                }

                if ( !( addButton.Tag is ComboBox comboBox ) )
                {
                    throw new ArgumentException( "Button doesn't have a proper ComboBox tag." );
                }

                if ( !( comboBox.SelectedItem is Component selectedComponent ) )
                {
                    return; // no selection
                }

                if ( !( comboBox.Tag is ListBox listBox ) )
                {
                    throw new ArgumentException( "ComboBox does not have a ListBox Tag." );
                }

                ThisGuidList.Add( selectedComponent.Guid );

                var convertedItems = new Converters.GuidListToComponentNames().Convert(
                    new object[] { ThisGuidList, ThisComponentList },
                    ThisGuidList.GetType(),
                    null,
                    CultureInfo.CurrentCulture
                ) as List<string>;

                listBox.Items = new AvaloniaList<object>( convertedItems ?? throw new InvalidOperationException() );
                listBox.InvalidateVisual();
            }
            catch ( Exception exception )
            {
                Logger.LogException( exception );
            }
        }

        private void RemoveFromList_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( !( sender is Button removeButton ) )
                {
                    throw new ArgumentException( "Sender is not a Button." );
                }

                if ( !( removeButton.Tag is ListBox listBox ) )
                {
                    throw new ArgumentException( "Button doesn't have a proper ListBox tag." );
                }

                int index = listBox.SelectedIndex;

                if ( index < 0 || index >= ThisGuidList.Count )
                {
                    return; // no selection
                }

                ThisGuidList.RemoveAt( index );

                var convertedItems = new Converters.GuidListToComponentNames().Convert(
                    new object[] { ThisGuidList, ThisComponentList },
                    ThisGuidList.GetType(),
                    null,
                    CultureInfo.CurrentCulture
                ) as List<string>;

                listBox.Items = new AvaloniaList<object>( convertedItems ?? throw new InvalidOperationException() );
                listBox.InvalidateVisual();
            }
            catch ( Exception exception )
            {
                Logger.LogException( exception );
            }
        }
    }
}
