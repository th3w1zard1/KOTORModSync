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
        public static void RemoveEmptySubcollections( object collection )
        {
            if ( !( collection is IEnumerable ) )
                return;

            Type enumerableType = collection.GetType();
            Type itemType = enumerableType.GetGenericArguments().FirstOrDefault();
            MethodInfo removeMethod = typeof( CollectionUtils ).GetMethod( "RemoveEmptySubcollections" );

            if ( itemType == null || removeMethod == null )
                return;

            MethodInfo method = removeMethod.MakeGenericMethod( itemType );
            _ = method.Invoke( obj: null, new[] { collection } );
        }

        public static void RemoveEmptySubcollections<T>( IEnumerable<T> collection )
        {
            if ( collection == null )
                return;

            var emptySubcollections = new List<object>();

            foreach ( T item in collection )
            {
                if ( item is IEnumerable subcollection && !subcollection.Cast<object>().Any() )
                    emptySubcollections.Add( subcollection );

                RemoveEmptySubcollections( item );
            }

            foreach ( object emptySubcollection in emptySubcollections )
            {
                var castedCollection = emptySubcollection as IEnumerable;

                foreach ( object unused in castedCollection.Cast<object>().ToArray() )
                {
                    _ = castedCollection.GetEnumerator().MoveNext();
                    castedCollection.GetEnumerator().Reset();
                }
            }
        }
    }

}
