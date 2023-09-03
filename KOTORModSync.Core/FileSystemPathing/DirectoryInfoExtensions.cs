// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KOTORModSync.Core.FileSystemPathing
{
	public static class DirectoryInfoExtensions
	{
		private static IEnumerable<T> SafeEnumerate<T>(IEnumerator<T> enumerator)
		{
			while ( true )
			{
				T thisEntry;
				try
				{
					if ( !enumerator.MoveNext() )
						break;

					thisEntry = enumerator.Current;
				}
				catch ( UnauthorizedAccessException permEx )
				{
					Logger.LogVerbose(
						$"Permission denied while enumerating file/folder wildcards: {permEx.Message} Skipping..."
					);
					continue; // Skip files or directories with access issues
				}
				catch ( IOException ioEx )
				{
					Logger.LogError(
						$"IO exception enumerating file/folder wildcards: {ioEx.Message} Skipping file/folder..."
					);
					continue; // Skip files or directories with IO issues
				}
				catch ( Exception ex )
				{
					Logger.LogError(
						$"Unhandled exception enumerating file/folder wildcards: {ex.Message}. Attempting to skip file/folder item..."
					);
					continue;
				}

				if ( thisEntry == null )
					continue;

				yield return thisEntry;
			}
		}

		public static IEnumerable<FileInfo> EnumerateFilesSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return SafeEnumerate(dirInfo.EnumerateFiles(searchPattern, searchOption).GetEnumerator());
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}

		public static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return SafeEnumerate(dirInfo.EnumerateDirectories(searchPattern, searchOption).GetEnumerator());
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}

		public static IEnumerable<FileSystemInfo> EnumerateFileSystemInfosSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return SafeEnumerate(dirInfo.EnumerateFileSystemInfos(searchPattern, searchOption).GetEnumerator());
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}

		public static DirectoryInfo[] GetDirectoriesSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return dirInfo.EnumerateDirectoriesSafely(searchPattern, searchOption).ToArray();
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}

		public static FileInfo[] GetFilesSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return dirInfo.EnumerateFilesSafely(searchPattern, searchOption).ToArray();
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}

		public static FileSystemInfo[] GetFileInfosSafely(
			this DirectoryInfo dirInfo,
			string searchPattern = "*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly
		)
		{
			try
			{
				return dirInfo.EnumerateFileSystemInfosSafely(searchPattern, searchOption).ToArray();
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}
	}
}
