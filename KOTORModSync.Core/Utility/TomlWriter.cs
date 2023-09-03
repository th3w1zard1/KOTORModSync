// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	public static class TomlWriter
	{
		[NotNull]
		public static string WriteString([NotNull] Dictionary<string, object> data)
		{
			if ( data.Count == 0 )
			{
				throw new ArgumentException(message: "Value cannot be null or an empty collection.", nameof( data ));
			}

			var sb = new StringBuilder();

			foreach ( KeyValuePair<string, object> kvp in data )
			{
				if ( kvp.Key == null )
					continue;

				WriteTomlKey(kvp.Key, kvp.Value, sb, indentLevel: 0);
			}

			return sb.ToString();
		}

		private static void WriteTomlKey(
			[NotNull] string key,
			[CanBeNull] object value,
			[NotNull] StringBuilder sb,
			int indentLevel
		)
		{
			if ( key == null )
				throw new ArgumentNullException(nameof( key ));
			if ( sb == null )
				throw new ArgumentNullException(nameof( sb ));

			if ( value == null )
				return;

			string indentation = new string(c: ' ', indentLevel * 4);

			switch ( value )
			{
				case Dictionary<string, object> table:
					{
						_ = sb.AppendLine($"{indentation}{key} = {{");
						foreach ( KeyValuePair<string, object> entry in table )
						{
							WriteTomlKey(entry.Key, entry.Value, sb, indentLevel + 1);
						}

						_ = sb.AppendLine($"{indentation}}}");
						break;
					}
				case List<object> array:
					{
						_ = sb.AppendLine($"{indentation}{key} = [");
						foreach ( object item in array )
						{
							if ( item is Dictionary<string, object> )
							{
								WriteTomlKey(string.Empty, item, sb, indentLevel + 1);
							}
							else
							{
								_ = sb.AppendLine($"{indentation}    {TomlValueToString(item)},");
							}
						}

						_ = sb.AppendLine($"{indentation}]");
						break;
					}
				default:
					_ = sb.AppendLine($"{indentation}{key} = {TomlValueToString(value)}");
					break;
			}
		}

		[CanBeNull]
		private static string TomlValueToString([NotNull] object value)
		{
			switch ( value )
			{
				case null:
					throw new ArgumentNullException(nameof( value ));
				case string str:
					return $"\"{EscapeTomlString(str)}\"";
				case bool boolean:
					return boolean
						? "true"
						: "false";
				case DateTime dateTime:
					return dateTime.ToString(format: "O");
				default:
					return value.ToString();
			}
		}

		[CanBeNull]
		private static string EscapeTomlString([CanBeNull] string str) =>
			str?.Replace(oldValue: "\\", newValue: "\\\\").Replace(oldValue: "\"", newValue: "\\\"")
				.Replace(oldValue: "\b", newValue: "\\b").Replace(oldValue: "\f", newValue: "\\f")
				.Replace(oldValue: "\n", newValue: "\\n").Replace(oldValue: "\r", newValue: "\\r")
				.Replace(oldValue: "\t", newValue: "\\t");
	}
}
