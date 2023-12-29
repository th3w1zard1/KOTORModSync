// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemPathing;

namespace KOTORModSync.Core.Utility
{
	public static class Utility
	{
		[NotNull]
		public static string ReplaceCustomVariables([NotNull] string path)
		{
			if ( path is null )
				throw new ArgumentNullException(nameof( path ));

			return path.Replace(
				oldValue: "<<modDirectory>>",
				newValue: MainConfig.SourcePath?.FullName
			).Replace(
				oldValue: "<<kotorDirectory>>",
				newValue: MainConfig.DestinationPath?.FullName
			);
		}

		[NotNull]
		public static string RestoreCustomVariables([NotNull] string fullPath)
		{
			if ( fullPath is null )
				throw new ArgumentNullException(nameof( fullPath ));

			return fullPath.Replace(
				oldValue: MainConfig.SourcePath?.FullName ?? string.Empty,
				newValue: "<<modDirectory>>"
			).Replace(
				oldValue: MainConfig.DestinationPath?.FullName ?? string.Empty,
				newValue: "<<kotorDirectory>>"
			);
		}

		public static bool IsRunningInsideAppBundle(string baseDirectory=null)
		{
			baseDirectory = baseDirectory ?? GetBaseDirectory();
			return baseDirectory.IndexOf(value: ".app/Contents/MacOS", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		[NotNull]
		public static string GetBaseDirectory()
		{
			string baseDirectory = Assembly.GetEntryAssembly()?.Location;
			return (
				!(baseDirectory is null)
					? Path.GetDirectoryName(baseDirectory)
					: Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
			) ?? AppDomain.CurrentDomain.BaseDirectory;
		}

		[NotNull]
		public static OSPlatform GetOS()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return OSPlatform.OSX;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return OSPlatform.Windows;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return OSPlatform.Linux;
			
			switch ( Environment.OSVersion.Platform )
			{
				case PlatformID.Win32NT:
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.WinCE:
				case PlatformID.Xbox:
					return OSPlatform.Windows;
				case PlatformID.MacOSX:
					return OSPlatform.OSX;
				case PlatformID.Unix:
					return OSPlatform.Linux;
				default:
					throw new Exception("Unknown/unsupported operating system, cannot continue");
			}
		}

		[NotNull]
		public static string GetResourcesDirectory(string baseDirectory=null)
		{
			baseDirectory = baseDirectory ?? GetBaseDirectory();

			if ( !IsRunningInsideAppBundle(baseDirectory) )
			{
				return Path.Combine(
					baseDirectory,
					path2: "Resources"
				);
			}
			
			// Navigate up two levels from 'MacOS' to get to KOTORModSync.app/Contents/MacOS/../../
			var directoryInfo = new DirectoryInfo(baseDirectory);
			if ( !(directoryInfo.Parent?.Parent is null) )
				baseDirectory = directoryInfo.Parent.Parent.FullName;

			return Path.Combine(
				baseDirectory,
				path2: "Resources"
			);

		}

		[CanBeNull]
		public static object GetEnumDescription([NotNull] Enum value)
		{
			if ( value is null )
				throw new ArgumentNullException(nameof( value ));

			Type type = value.GetType();
			string name = Enum.GetName(type, value);
			if ( name is null )
				return null;

			FieldInfo field = type.GetField(name);

			DescriptionAttribute attribute = field?.GetCustomAttribute<DescriptionAttribute>();
			return attribute?.Description ?? name;
		}

		public static bool IsDirectoryWritable([NotNull] DirectoryInfo dirPath)
		{
			if ( dirPath is null )
				throw new ArgumentNullException(nameof( dirPath ));

			try
			{
				using ( File.Create(
						Path.Combine(PathHelper.GetCaseSensitivePath(dirPath).FullName, Path.GetRandomFileName()),
						bufferSize: 1,
						FileOptions.DeleteOnClose
					) ) { }

				return true;
			}
			catch ( UnauthorizedAccessException ex )
			{
				Logger.LogError($"Failed to access files in the destination directory: {ex.Message}");
			}
			catch ( PathTooLongException ex )
			{
				Logger.LogException(ex);
				Logger.LogError($"The pathname is too long: '{dirPath.FullName}'");
				Logger.LogError(
					"Please utilize the registry patch that increases the Windows legacy path limit higher than 260 characters"
					+ " or move your folder/file above to a shorter directory path."
				);
			}
			catch ( IOException ex )
			{
				Logger.LogError($"Failed to access files in the destination directory: {ex.Message}");
			}

			return false;
		}

		[CanBeNull]
		public static DirectoryInfo ChooseDirectory()
		{
			Console.Write("Enter the path: ");
			string thisPath = Console.ReadLine();
			if ( string.IsNullOrEmpty(thisPath) )
				return default;

			thisPath = thisPath.Trim();

			if ( Directory.Exists(thisPath) )
				return new DirectoryInfo(thisPath);

			Console.Write($"Directory '{thisPath}' does not exist.");
			return default;
		}
	}
}
