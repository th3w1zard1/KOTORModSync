// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;

namespace KOTORModSync.Core.Utility
{
    // ReSharper disable once InconsistentNaming
    public static class IEnumerableExtensions
    {
	    // Extension method to check if an IEnumerable is null or has no contents
	    public static bool IsNullOrEmptyCollection<T>(this IEnumerable<T> collection) => IsNullOrEmptyCollectionInternal(collection);
	    public static bool IsNullOrEmptyCollection(this IEnumerable collection) => IsNullOrEmptyCollectionInternal(collection);

	    // Extension methods to check if an IEnumerable contains only null entries
	    public static bool IsNullOrEmptyOrAllNull<T>(this IEnumerable<T> collection) => IsNullOrEmptyOrAllNullInternal(collection);
	    public static bool IsNullOrEmptyOrAllNull(this IEnumerable collection) => IsNullOrEmptyOrAllNullInternal(collection);


	    // Helper method to check if an IEnumerable is null or has no contents
	    private static bool IsNullOrEmptyCollectionInternal(IEnumerable collection)
        {
            if (collection == null)
                return true;
            if (collection is ICollection col)
                return col.Count == 0;

            return !collection.GetEnumerator().MoveNext();
        }


	    // Helper method to check if an IEnumerable contains only null entries
	    private static bool IsNullOrEmptyOrAllNullInternal(IEnumerable collection)
        {
            if (collection == null)
                return true;

            foreach (object item in collection)
            {
                if (item != null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
