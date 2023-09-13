// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core.FileSystemPathing
{
	[SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
	public class CaseAwarePath
	{
		private string _str;

		public CaseAwarePath(params string[] args) :
			this((object[])args) { } // Call the more general constructor directly

		public CaseAwarePath(params object[] args)
		{
			_str = CombinePaths(args);
			_str = FixPathFormatting(_str);

			if ( ShouldResolveCase(_str) )
			{
				_str = GetCaseSensitivePath();
			}


			string[] parts = _str.Split(Path.DirectorySeparatorChar);
			var result = new CaseAwarePath[parts.Length];
			for ( int i = 0; i < parts.Length; i++ )
			{
				string cumulativePath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1));
				result[i] = new CaseAwarePath(cumulativePath);
			}

			Name = Path.GetFileName(_str);
			Parent = result.Length >= 2
				? result[result.Length - 2]
				: null;
			Parts = result;
			Root = Path.GetPathRoot(_str);
			Stem = Path.GetFileNameWithoutExtension(_str);
			Suffix = Path.GetExtension(_str);
			//Drive
			//Anchor
			//Parents
		}


		public string Name { get; }
		public CaseAwarePath Parent { get; set; }
		public CaseAwarePath[] Parts { get; }
		public string Root { get; set; }
		public string Stem { get; set; }
		public string Suffix { get; set; }

		private static string ConvertObjectToPath(object arg)
		{
			switch ( arg )
			{
				case CaseAwarePath cap:
					return cap.ToString();
				case FileSystemInfo fsi:
					return fsi.FullName;
				case string s:
					return s;
				default:
					throw new ArgumentException($"Unsupported type: {arg.GetType().FullName}");
			}
		}


		private static string CombinePaths(params object[] args) =>
			Path.Combine(args.Select(ConvertObjectToPath).ToArray());

		public static CaseAwarePath operator /(CaseAwarePath p1, object p2) => p1.JoinPath(p2);

		public static CaseAwarePath operator /(object p1, CaseAwarePath p2) => new CaseAwarePath(p1).JoinPath(p2);

		public CaseAwarePath JoinPath(params object[] args)
		{
			var paths = args.Select(ConvertObjectToPath).ToList();

			// If the base path (_str) is not rooted, simply combine all and return.
			if ( !Path.IsPathRooted(_str) )
			{
				return new CaseAwarePath(
					Path.Combine(_str, string.Join(Path.DirectorySeparatorChar.ToString(), paths))
				);
			}

			string accumulatedPath = _str; // Start with the current instance path.

			foreach ( string currentPath in paths )
			{
				// If path is rooted and contains the accumulated path hierarchy, intelligently combine.
				if ( Path.IsPathRooted(currentPath)
					&& currentPath.StartsWith(accumulatedPath, StringComparison.OrdinalIgnoreCase) )
				{
					accumulatedPath = currentPath; // Take the most detailed path.
				}
				else
				{
					accumulatedPath = Path.Combine(
						accumulatedPath,
						currentPath
					); // Append both non-rooted paths and rooted ones that don't fit the accumulated hierarchy.
				}
			}

			return new CaseAwarePath(accumulatedPath);
		}


		public CaseAwarePath Resolve()
		{
			_str = Path.GetFullPath(_str);
			if ( ShouldResolveCase(_str) )
			{
				_str = GetCaseSensitivePath();
			}

			return this;
		}

		private string GetCaseSensitivePath()
		{
			var parts = _str.Split(Path.DirectorySeparatorChar).ToList();

			for ( int i = 1; i < parts.Count; i++ )
			{
				string basePath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i));
				string nextPath = Path.Combine(basePath, parts[i]);

				if ( !Directory.Exists(nextPath) && Directory.Exists(basePath) )
				{
					int i1 = i;
					parts[i] = FindClosestMatch(
						parts[i],
						Directory.GetFileSystemEntries(basePath).Where(x => i1 == parts.Count - 1 || Directory.Exists(x))
					);
				}
				else if ( !File.Exists(nextPath) && !Directory.Exists(nextPath) )
				{
					return Path.Combine(basePath, string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(i)));
				}
			}

			return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
		}

		private static string FindClosestMatch(string target, IEnumerable<string> candidates)
		{
			int maxMatchingChars = -1;
			string closestMatch = target;

			foreach ( string candidate in candidates )
			{
				int matchingChars = GetMatchingCharactersCount(Path.GetFileName(candidate), target);
				if ( matchingChars > maxMatchingChars )
				{
					maxMatchingChars = matchingChars;
					closestMatch = candidate;
					if ( maxMatchingChars == target.Length )
						break;
				}
			}

			return closestMatch;
		}

		private static int GetMatchingCharactersCount(string str1, string str2)
		{
			if ( string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase) )
				return -1;

			return str1.Zip(
				str2,
				(c1, c2) => c1 == c2
					? 1
					: 0
			).Sum();
		}

		private static string FixPathFormatting(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return path;

			string formattedPath = path.Replace(oldValue: "\\", Path.DirectorySeparatorChar.ToString())
				.Replace(oldValue: "/", Path.DirectorySeparatorChar.ToString());

			formattedPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Regex.Replace(formattedPath, pattern: @"\\{2,}", replacement: @"\")
				: Regex.Replace(formattedPath, pattern: @"/{2,}", replacement: "/");

			return formattedPath.TrimEnd(Path.DirectorySeparatorChar);
		}

		private static bool ShouldResolveCase(string path) =>
			!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			&& Path.IsPathRooted(path)
			&& !File.Exists(path)
			&& !Directory.Exists(path);

		public override string ToString() => _str;
	}
}
