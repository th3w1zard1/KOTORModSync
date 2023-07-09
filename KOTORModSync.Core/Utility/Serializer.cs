// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Tomlyn.Model;

// ReSharper disable UnusedMember.Global
#pragma warning disable RCS1213, IDE0051, IDE0079

namespace KOTORModSync.Core.Utility
{
    [SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" )]
    public static class Serializer
    {
        [NotNull]
        public static string FixGuidString( string guidString )
        {
            // Remove any whitespace characters
            guidString = Regex.Replace( guidString, @"\s", "" );

            // Remove all non-base16 characters.
            guidString = Regex.Replace( guidString, @"[^0-9A-Fa-f]", "" );

            // not even close to a guid.
            if ( guidString.Length != 32 )
            {
                return Guid.Empty.ToString();
            }

            // Insert necessary dashes between the GUID sections
            guidString = Regex.Replace( guidString, @"(\w{8})(\w{4})(\w{4})(\w{4})(\w{12})", "$1-$2-$3-$4-$5" );

            // Attempt to fix common issues with GUID strings
            if ( !guidString.StartsWith( "{", StringComparison.Ordinal ) )
            {
                guidString = "{" + guidString;
            }

            if ( !guidString.EndsWith( "}", StringComparison.Ordinal ) ) guidString += "}";


            return guidString;
        }

        public static void DeserializePathInDictionary( Dictionary<string, object> dict, string key )
        {
            if ( !dict.TryGetValue( key, out object pathValue ) )
            {
                return;
            }

            switch ( pathValue )
            {
                case string path:
                    {
                        string formattedPath = FixPathFormatting( path );
                        dict[key] = new List<string> { PrefixPath( formattedPath ) };
                        break;
                    }
                case IList<string> paths:
                    {
                        for ( int index = 0; index < paths.Count; index++ )
                        {
                            string currentPath = paths[index];
                            string formattedPath = FixPathFormatting( currentPath );
                            paths[index] = PrefixPath( formattedPath );
                        }

                        break;
                    }
            }
        }

        public static void DeserializeGuidDictionary( [NotNull] Dictionary<string, object> dict, [NotNull] string key )
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
                        var stringList = new List<string>( 65535 ) { stringValue };

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
            !path.StartsWith( "<<modDirectory>>" ) && !path.StartsWith( "<<kotorDirectory>>" )
                ? FixPathFormatting( "<<modDirectory>>" + Environment.NewLine + path )
                : path;

        [NotNull]
        public static string FixPathFormatting( [NotNull] string path )
        {
            // Replace backslashes with forward slashes
            string formattedPath = path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar )
                .Replace( '\\', Path.DirectorySeparatorChar )
                .Replace( '/', Path.DirectorySeparatorChar );

            // Fix repeated slashes
            formattedPath = Regex.Replace(
                formattedPath,
                $"(?<!:){Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}+",
                Path.DirectorySeparatorChar.ToString()
            );

            // Fix trailing slashes
            formattedPath = formattedPath.TrimEnd( Path.DirectorySeparatorChar );

            return formattedPath;
        }

        [NotNull]
        public static Dictionary<string, object> ConvertTomlTableToDictionary( [NotNull] TomlTable tomlTable )
        {
            var dict = new Dictionary<string, object>( 65535 );

            foreach ( KeyValuePair<string, object> kvp in tomlTable )
            {
                string key = kvp.Key.ToLowerInvariant();
                object value = kvp.Value;

                if ( value is TomlTable nestedTable )
                {
                    dict.Add( key, ConvertTomlTableToDictionary( nestedTable ) );
                }
                else
                {
                    dict.Add( key, value );
                }
            }

            return dict;
        }

        [NotNull]
        public static string FixWhitespaceIssues( [NotNull] string strContents )
        {
            strContents = strContents.Replace( "\r\n", "\n" )
                .Replace( "\r", Environment.NewLine )
                .Replace( "\n", Environment.NewLine );

            string[] lines = Regex.Split( strContents, $"(?<!\r){Regex.Escape( Environment.NewLine )}" )
                .Select( line => line.Trim() )
                .ToArray();

            return string.Join( Environment.NewLine, lines );
        }

        [NotNull]
        public static List<object> CreateMergedList( [NotNull] params IEnumerable<object>[] lists )
        {
            var mergedList = new List<object>( 65535 );

            foreach ( IEnumerable<object> list in lists )
            {
                mergedList.AddRange( list );
            }

            return mergedList;
        }

        [NotNull]
        public static IEnumerable<object> EnumerateDictionaryEntries( [NotNull] IEnumerator enumerator )
        {
            while ( enumerator.MoveNext() )
            {
                if ( enumerator.Current is null )
                {
                    continue;
                }

                var entry = (DictionaryEntry)enumerator.Current;
                yield return new KeyValuePair<object, object>( entry.Key, entry.Value );
            }
        }

        [CanBeNull]
        public static object SerializeObject( [CanBeNull] object obj )
        {
            Type type = obj.GetType();

            switch ( obj )
            {
                // do nothing if it's already a simple type.
                case IConvertible _:
                case IFormattable _:
                case IComparable _:
                    return obj.ToString();
                // handle generic list types
                case IList objList:
                    return SerializeList( objList );
            }

            var serializedProperties = new Dictionary<string, object>();

            IEnumerable<object> members;
            switch ( obj )
            {
                // IDictionary types
                case IDictionary mainDictionary:
                    IEnumerator enumerator = mainDictionary.GetEnumerator();
                    members = EnumerateDictionaryEntries( enumerator );
                    break;

                // class instance types
                default:
                    members = type.GetMembers(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                    );
                    break;
            }

            foreach ( object member in members )
            {
                object value = null;
                string memberName = null;

                switch ( member )
                {
                    case KeyValuePair<object, object> dictionaryEntry:
                        memberName = dictionaryEntry.Key.ToString();
                        value = dictionaryEntry.Value;
                        break;
                    case PropertyInfo property when property.CanRead
                        && !property.GetMethod.IsStatic
                        && !Attribute.IsDefined( property, typeof( JsonIgnoreAttribute ) )
                        && property.DeclaringType == obj.GetType():
                        {
                            value = property.GetValue( obj );
                            memberName = property.Name;
                            break;
                        }
                    case FieldInfo field
                        when !field.IsStatic && !Attribute.IsDefined( field, typeof( JsonIgnoreAttribute ) ):
                        {
                            value = field.GetValue( obj );
                            memberName = field.Name;
                            break;
                        }
                }

                switch ( value )
                {
                    case null:
                        continue;
                    case string valueStr:
                        serializedProperties[memberName] = valueStr;
                        break;
                    case IDictionary dictionary:
                        {
                            var tomlTable = new TomlTable();

                            foreach ( DictionaryEntry entry in dictionary )
                            {
                                string key = entry.Key.ToString();
                                object value2 = SerializeObject( entry.Value );
                                tomlTable.Add( key, value2 );
                            }

                            serializedProperties[memberName] = tomlTable;

                            break;
                        }

                    case IList list:
                        {
                            serializedProperties[memberName] = SerializeList( list );
                            break;
                        }
                    default:
                        {
                            if ( value.GetType().IsNested )
                            {
                                serializedProperties[memberName] = SerializeObject( value );
                                continue;
                            }

                            serializedProperties[memberName] = value.ToString();

                            break;
                        }
                }
            }

            return serializedProperties.Count > 0
                ? serializedProperties
                : (object)obj.ToString();
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        [CanBeNull]
        private static TomlArray SerializeList( [CanBeNull][ItemCanBeNull] IList list )
        {
            var serializedList = new TomlArray();

            if ( list is null )
            {
                return serializedList;
            }

            foreach ( object item in list )
            {
                if ( item is null )
                {
                    continue;
                }

                if ( item.GetType().IsPrimitive || item is string )
                {
                    serializedList.Add( item.ToString() );
                    continue;
                }

                if ( item is IList nestedList )
                {
                    serializedList.Add( SerializeList( nestedList ) );
                }
                else
                {
                    serializedList.Add( SerializeObject( item ) );
                }
            }

            return serializedList;
        }

        /*
        public static bool IsNonClassEnumerable( object obj )
        {
            Type type = obj.GetType();

            // Check if the type is assignable from IEnumerable, not a string, and not a class instance
            bool isNonClassEnumerable = typeof( IEnumerable ).IsAssignableFrom( type )
                && type != typeof( string )
                && ( !type.IsClass || type.IsSealed || type.IsAbstract );

            // Check if the object is a custom type (excluding dynamic types and proxy objects)
            bool isCustomType = type.FullName != null
                && ( type.Assembly.FullName.StartsWith( "Dynamic" )
                    || type.FullName.Contains( "__TransparentProxy" ) );
            isNonClassEnumerable &= !isCustomType;

            return isNonClassEnumerable;
        }*/
    }
}
