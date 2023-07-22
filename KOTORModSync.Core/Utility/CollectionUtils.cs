// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KOTORModSync.Core.Utility
{
    public static class CollectionUtils
    {
        public static void RemoveEmptySubCollections( object collection )
        {
            if ( !( collection is IEnumerable ) )
                return;

            Type enumerableType = collection.GetType();
            Type itemType = enumerableType.GetGenericArguments().FirstOrDefault();
            MethodInfo removeMethod = typeof( CollectionUtils ).GetMethod( "RemoveEmptySubCollections" );

            if ( itemType == null || removeMethod == null )
                return;

            MethodInfo method = removeMethod.MakeGenericMethod( itemType );
            _ = method.Invoke( obj: null, new[] { collection } );
        }

        public static void RemoveEmptySubCollections<T>( IEnumerable<T> collection )
        {
            if ( collection == null )
                return;

            var emptySubCollections = new List<object>();

            foreach ( T item in collection )
            {
                if ( item is IEnumerable subCollection && !subCollection.Cast<object>().Any() )
                    emptySubCollections.Add( subCollection );

                RemoveEmptySubCollections( item );
            }

            foreach ( object emptySubCollection in emptySubCollections )
            {
                var castedCollection = emptySubCollection as IEnumerable;

                for ( int i = 0; i < castedCollection.Cast<object>().ToArray().Length; i++ )
                {
                    _ = castedCollection.GetEnumerator().MoveNext();
                    castedCollection.GetEnumerator().Reset();
                }
            }
        }
    }
}
