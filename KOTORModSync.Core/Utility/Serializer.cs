// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemPathing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable UnusedMember.Global
namespace KOTORModSync.Core.Utility
{
    public static class Serializer
    {
	    [NotNull]
        public static string FixGuidString( [NotNull] string guidString )
        {
            if ( string.IsNullOrWhiteSpace( guidString ) )
                throw new ArgumentException( message: "Value cannot be null or whitespace.", nameof( guidString ) );

            // Remove any whitespace characters
            guidString = Regex.Replace( guidString, pattern: @"\s", replacement: "" );

            // Remove all non-base16 characters.
            guidString = Regex.Replace( guidString, pattern: "[^0-9A-Fa-f]", replacement: "" );

            // not even close to a guid.
            if ( guidString.Length != 32 )
            {
                return Guid.Empty.ToString();
            }

            // Insert necessary dashes between the GUID sections
            guidString = Regex.Replace(
                guidString,
                pattern: @"(\w{8})(\w{4})(\w{4})(\w{4})(\w{12})",
                replacement: "$1-$2-$3-$4-$5"
            );

            // Attempt to fix common issues with GUID strings
            if ( !guidString.StartsWith( value: "{", StringComparison.Ordinal ) )
            {
                guidString = "{" + guidString;
            }

            if ( !guidString.EndsWith( value: "}", StringComparison.Ordinal ) ) guidString += "}";

            return guidString;
        }

        // converts accidental lists into strings and vice versa
        public static void DeserializePathInDictionary(
            [NotNull] IDictionary<string, object> dict,
            [NotNull] string key
        )
        {
            if ( dict.Count == 0 )
                throw new ArgumentException( message: "Value cannot be null or empty.", nameof( dict ) );
            if ( string.IsNullOrEmpty( key ) )
                throw new ArgumentException( message: "Value cannot be null or empty.", nameof( key ) );

            if ( !dict.TryGetValue( key, out object pathValue ) )
            {
                return;
            }

            switch ( pathValue )
            {
                case string path:
                    {
                        string formattedPath = PathHelper.FixPathFormatting( path );
                        dict[key] = new List<string>
                        {
                            PrefixPath( formattedPath ),
                        };
                        break;
                    }
                case IList<string> paths:
                    {
                        for ( int index = 0; index < paths.Count; index++ )
                        {
                            string currentPath = paths[index];
                            string formattedPath = PathHelper.FixPathFormatting( currentPath );
                            paths[index] = PrefixPath( formattedPath );
                        }

                        break;
                    }
            }
        }

        // converts accidental lists into strings and vice versa
        public static void DeserializeGuidDictionary( [NotNull] IDictionary<string, object> dict, [NotNull] string key )
        {
            if ( !dict.TryGetValue( key, out object value ) )
            {
                return;
            }

            switch ( value )
            {
                case string stringValue:
                    {
                        // Convert the string to a list of strings
                        var stringList = new List<string>
                        {
                            stringValue,
                        };

                        // Replace the string value with the list
                        dict[key] = stringList;

                        // Fix GUID strings in each list item
                        for ( int i = 0; i < stringList.Count; i++ )
                        {
                            if ( Guid.TryParse( stringList[i], out Guid guid ) )
                            {
                                continue;
                            }

                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString( guid.ToString() );

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }

                        break;
                    }
                case List<string> stringList:
                    {
                        // Fix GUID strings in each list item
                        for ( int i = 0; i < stringList.Count; i++ )
                        {
                            if ( Guid.TryParse( stringList[i], out Guid guid ) )
                            {
                                continue;
                            }

                            // Attempt to fix common issues with GUID strings
                            string fixedGuid = FixGuidString( guid.ToString() );

                            // Update the list item with the fixed GUID string
                            stringList[i] = fixedGuid;
                        }

                        break;
                    }
            }
        }

        [NotNull]
        public static string PrefixPath( [NotNull] string path ) =>
            string.IsNullOrWhiteSpace( path )
                ? throw new ArgumentException( message: "Value cannot be null or whitespace.", nameof( path ) )
                : !path.StartsWith( value: "<<modDirectory>>", StringComparison.OrdinalIgnoreCase )
                && !path.StartsWith( value: "<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase )
                    ? PathHelper.FixPathFormatting( "<<modDirectory>>" + Path.DirectorySeparatorChar + path )
                    : path;

        [NotNull]
        public static string FixWhitespaceIssues( [NotNull] string strContents )
        {
            strContents = strContents.Replace( oldValue: "\r\n", newValue: "\n" )
                .Replace( oldValue: "\r", Environment.NewLine )
                .Replace( oldValue: "\n", Environment.NewLine );

            string[] lines = Regex.Split( strContents, $"(?<!\r){Regex.Escape( Environment.NewLine )}" )
                .Select( line => line?.Trim() )
                .ToArray();

            return string.Join( Environment.NewLine, lines );
        }

        [NotNull]
        public static List<object> CreateMergedList( [NotNull] params IEnumerable<object>[] lists )
        {
            var mergedList = new List<object>();

            foreach ( IEnumerable<object> list in lists )
            {
                if ( list is null )
                    continue;

                mergedList.AddRange( list );
            }

            return mergedList;
        }

        [NotNull]
        [ItemNotNull]
        public static IEnumerable<object> EnumerateDictionaryEntries( [NotNull] IEnumerator enumerator )
        {
            while ( enumerator.MoveNext() )
            {
                if ( enumerator.Current is null )
                    continue;

                var entry = (DictionaryEntry)enumerator.Current;
                yield return new KeyValuePair<object, object>( entry.Key, entry.Value );
            }
        }

        [CanBeNull]
        public static object SerializeObject( [CanBeNull] object obj )
        {
            switch ( obj )
            {
                case null:
                    return null;
                case IConvertible _:
                case IFormattable _:
                case IComparable _:
                    return obj.ToString();
                case IList objList:
                    return SerializeIntoList( objList );
                case IDictionary _:
                case var _ when obj.GetType().IsClass:
                    return SerializeIntoDictionary( obj );
                default:
                    return obj.ToString();
            }
        }

        [NotNull]
        internal static Dictionary<string, object> SerializeIntoDictionary( [CanBeNull] object obj )
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, NullValueHandling = NullValueHandling.Ignore,
            };

            string jsonString = JsonConvert.SerializeObject( obj, settings );
            var jsonObject = JObject.Parse( jsonString );

            return ConvertJObjectToDictionary( jsonObject );
        }

        [CanBeNull]
        private static object ConvertJTokenToObject( [CanBeNull] JToken token )
        {
            switch ( token )
            {
                case JObject jObject:
                    return ConvertJObjectToDictionary( jObject );
                case JArray jArray:
                    return ConvertJArrayToList( jArray );
                default:
                    return ( (JValue)token )?.Value;
            }
        }

        [NotNull]
        private static List<object> ConvertJArrayToList( [NotNull] JArray jArray ) =>
            jArray is null
                ? throw new ArgumentNullException( nameof( jArray ) )
                : jArray.Select( ConvertJTokenToObject )
                    .ToList();

        [NotNull]
        private static Dictionary<string, object> ConvertJObjectToDictionary( [NotNull] JObject jObject ) =>
            jObject is null
                ? throw new ArgumentNullException( nameof( jObject ) )
                : jObject.Properties()
                    .ToDictionary( property => property.Name, property => ConvertJTokenToObject( property.Value ) );

        [CanBeNull]
        public static List<object> SerializeIntoList( [CanBeNull] object obj )
        {
            // Use Newtonsoft.Json for serialization and deserialization
            string jsonString = JsonConvert.SerializeObject( obj );
            return JsonConvert.DeserializeObject<List<object>>( jsonString );
        }
    }
}
