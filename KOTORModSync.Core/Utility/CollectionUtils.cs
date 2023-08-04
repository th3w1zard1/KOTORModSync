// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class CollectionUtils
    {
        public static void RemoveEmptyCollections( [NotNull] IDictionary<string, object> thisTable )
        {
            if ( thisTable is null )
                throw new ArgumentNullException( nameof( thisTable ) );

            var itemsToRemove = new List<string>();

            foreach ( KeyValuePair<string, object> kvp in thisTable )
            {
                if ( kvp.Key == null )
                    continue;

                switch ( kvp.Value )
                {
                    case null:
                        itemsToRemove.Add( kvp.Key );
                        continue;
                    case IEnumerable enumerable when !enumerable.GetEnumerator().MoveNext():
                        itemsToRemove.Add( kvp.Key );
                        continue;
                    case IDictionary<string, object> table:
                        {
                            var emptyKeys = table.Keys.Where(
                                key =>
                                {
                                    if ( key is null )
                                        return default;

                                    object value = table[key];
                                    switch ( value )
                                    {
                                        case IList<object> list:
                                            RemoveEmptyCollections( list ); // Recursively check sub-lists
                                            return list.IsNullOrEmptyOrAllNull();
                                        case IDictionary<string, object> dict:
                                            RemoveEmptyCollections( dict ); // Recursively check sub-dictionaries
                                            return dict.IsNullOrEmptyOrAllNull();
                                        default:
                                            return false;
                                    }
                                }
                            ).ToList();

                            foreach ( string key in emptyKeys )
                            {
                                if ( key is null )
                                    continue;

                                _ = table.Remove( key );
                            }

                            if ( table.Count == 0 )
                                itemsToRemove.Add( table.GetHashCode().ToString() );

                            break;
                        }
                    case IList<object> list:
                        RemoveEmptyCollections( list ); // Recursively check sub-lists
                        break;
                }
            }

            // Remove items from the rootTable
            foreach ( string item in itemsToRemove )
            {
                if ( item is null )
                    continue;

                _ = thisTable.Remove( item );
            }
        }

        public static void RemoveEmptyCollections( [NotNull][ItemCanBeNull] IList<object> list )
        {
            if ( list is null )
                throw new ArgumentNullException( nameof( list ) );

            for ( int i = list.Count - 1; i >= 0; i-- )
            {
                object item = list[i];
                switch ( item )
                {
                    case null:
                        list.RemoveAt( i );
                        continue;
                    case IDictionary<string, object> dict:
                        {
                            RemoveEmptyCollections( dict ); // Recursively check sub-dictionaries
                            if ( dict.IsNullOrEmptyCollection() )
                                list.RemoveAt( i );

                            break;
                        }
                    case IList<object> subList:
                        {
                            RemoveEmptyCollections( subList ); // Recursively check sub-lists
                            if ( subList.IsNullOrEmptyCollection() )
                                list.RemoveAt( i );

                            break;
                        }
                }
            }
        }
    }
}
