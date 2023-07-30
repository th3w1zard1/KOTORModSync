// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync.Controls
{
    public partial class DependencyControl : UserControl
    {
        public DependencyControl()
        {
            InitializeComponent();
            DependenciesListBox = this.FindControl<ListBox>( "DependenciesListBox" );
        }

        // used to fix the move window code with combo boxes.
        protected override void OnAttachedToVisualTree( VisualTreeAttachmentEventArgs e )
        {
            base.OnAttachedToVisualTree( e );

            if ( VisualRoot is MainWindow mainWindow )
                mainWindow.FindComboBoxesInWindow( mainWindow );
        }

        public event EventHandler<PropertyChangedEventArgs> PropertyChanged2;
        private string _searchText;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if ( _searchText == value )
                    return; // prevent recursion problems

                _searchText = value;
                PropertyChanged2?.Invoke( this, new PropertyChangedEventArgs( nameof( SearchText ) ) );
            }
        }

        private void SearchText_PropertyChanged( object sender, PropertyChangedEventArgs e )
        {
            if ( e.PropertyName != nameof( SearchText ) )
                return;

            if ( !( VisualRoot is MainWindow mainWindow ) )
                throw new NullReferenceException( "Could not get main window instance" );

            string searchText = SearchText;
            MainWindow.FilterControlListItems( DependenciesListBox, searchText );
        }

        [NotNull]
        public static readonly StyledProperty<List<Guid>> ThisGuidListProperty
            = AvaloniaProperty.Register<DependencyControl, List<Guid>>( nameof( ThisGuidList ) );

        [NotNull]
        public List<Guid> ThisGuidList
        {
            get => GetValue( ThisGuidListProperty )
                ?? throw new NullReferenceException( "Could not retrieve property 'ThisGuidListProperty'" );
            set => SetValue( ThisGuidListProperty, value );
        }

        [NotNull]
        public static readonly StyledProperty<List<Component>> ThisComponentListProperty
            = AvaloniaProperty.Register<DependencyControl, List<Component>>( nameof( ThisComponentList ) );

        [NotNull]
        public List<Component> ThisComponentList
        {
            get => GetValue( ThisComponentListProperty )
                ?? throw new NullReferenceException( "Could not retrieve property 'ThisComponentListProperty'" );
            set => SetValue( ThisComponentListProperty, value );
        }

        private void AddToList_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
        {
            try
            {
                if ( !( sender is Button addButton ) )
                    throw new ArgumentException( "Sender is not a Button." );

                if ( !( addButton.Tag is ComboBox comboBox ) )
                    throw new ArgumentException( "Button doesn't have a proper ComboBox tag." );

                if ( !( comboBox.SelectedItem is Component selectedComponent ) )
                    return; // no selection

                if ( !( comboBox.Tag is ListBox listBox ) )
                    throw new ArgumentException( "ComboBox does not have a ListBox Tag." );

                ThisGuidList.Add( selectedComponent.Guid );

                var convertedItems = new Converters.GuidListToComponentNames().Convert(
                    new object[]
                    {
                        ThisGuidList, ThisComponentList,
                    },
                    ThisGuidList.GetType(),
                    parameter: null,
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
                    throw new ArgumentException( "Sender is not a Button." );

                if ( !( removeButton.Tag is ListBox listBox ) )
                    throw new ArgumentException( "Button doesn't have a proper ListBox tag." );

                int index = listBox.SelectedIndex;

                if ( index < 0 || index >= ThisGuidList.Count )
                    return; // no selection

                ThisGuidList.RemoveAt( index );

                var convertedItems = new Converters.GuidListToComponentNames().Convert(
                    new object[]
                    {
                        ThisGuidList, ThisComponentList,
                    },
                    ThisGuidList.GetType(),
                    parameter: null,
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
