// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace KOTORModSync.Core.Utility
{
	public static class ArchiveHelper
	{
		public static readonly ExtractionOptions DefaultExtractionOptions = new ExtractionOptions
		{
			ExtractFullPath = false, Overwrite = true, PreserveFileTime = true,
		};

		public static bool IsArchive([NotNull] string filePath) => IsArchive(
			new FileInfo(filePath ?? throw new ArgumentNullException(nameof( filePath )))
		);

		public static bool IsArchive([NotNull] FileInfo thisFile) =>
			thisFile.Extension.Equals(value: ".zip", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".7z", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".rar", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".exe", StringComparison.OrdinalIgnoreCase);

		public static (IArchive, FileStream) OpenArchive(string archivePath)
		{
			if ( archivePath is null || !File.Exists(archivePath) )
				throw new ArgumentException(message: "Path must be a valid file on disk.", nameof( archivePath ));

			try
			{
				FileStream stream = File.OpenRead(archivePath);
				IArchive archive = null;

				if ( archivePath.EndsWith(value: ".zip", StringComparison.OrdinalIgnoreCase) )
				{
					archive = ZipArchive.Open(stream);
				}
				else if ( archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase) )
				{
					archive = RarArchive.Open(stream);
				}
				else if ( archivePath.EndsWith(value: ".7z", StringComparison.OrdinalIgnoreCase) )
				{
					archive = SevenZipArchive.Open(stream);
				}

				return (archive, stream);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return (null, null);
			}
		}

		//todo: Check() seems to return true on all files regardless of content?
		public static bool IsValidArchive([CanBeNull] string filePath)
		{
			if ( filePath is null || !File.Exists(filePath) )
				return false;

			try
			{
				SevenZipBase.SetLibraryPath( Path.Combine(Utility.GetResourcesDirectory(), "7z.dll") ); // Path to 7z.dll
				bool valid;
				using ( var extractor = new SevenZipExtractor(filePath) )
				{
					// The Check() method throws an exception if the archive is invalid.
					valid = extractor.Check();
				}

				if ( !valid )
					valid = IsPotentialSevenZipSFX(filePath);
				return valid;
			}
			catch ( Exception )
			{
				// Here we catch the exception if it's not a valid archive.
				// We'll then check if it's an SFX.
				return IsPotentialSevenZipSFX(filePath);
			}
		}

		public static bool IsPotentialSevenZipSFX([NotNull] string filePath)
		{
			// These bytes represent a typical signature for Windows executables. 
			byte[] sfxSignature =
			{
				0x4D, 0x5A,
			}; // 'MZ' header

			byte[] fileHeader = new byte[sfxSignature.Length];

			using ( var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read) )
			{
				_ = fs.Read(fileHeader, offset: 0, sfxSignature.Length);
			}

			return sfxSignature.SequenceEqual(fileHeader);
		}

		public static string AnalyzeArchiveForExe(FileStream fileStream, IArchive archive)
		{
			string exePath = null;
			bool tslPatchDataFolderExists = false;

			using (IReader reader = archive.ExtractAllEntries())
			{
				while (reader.MoveToNextEntry())
				{
					if ( reader.Entry.IsDirectory )
						continue;

					string fileName = Path.GetFileName(reader.Entry.Key);
					string directory = Path.GetDirectoryName(reader.Entry.Key);

					// Check for exe file
					if (fileName.EndsWith(value: ".exe", StringComparison.OrdinalIgnoreCase))
					{
						if (exePath != null)
							return null;  // Multiple exe files found in the archive.

						exePath = reader.Entry.Key;
					}

					// Check for 'tslpatchdata' folder
					if (!(directory is null) && directory.Contains("tslpatchdata"))
					{
						tslPatchDataFolderExists = true;
					}
				}
			}

			if (
				exePath != null
				&& tslPatchDataFolderExists 
				// ReSharper disable once PossibleNullReferenceException
				&& Path.GetDirectoryName(exePath).Contains("tslpatchdata")
			)
			{
				return exePath;
			}

			return null;
		}

		public static void ExtractWith7Zip(FileStream stream, string destinationDirectory)
		{
			if ( !(Utility.GetOperatingSystem() == OSPlatform.Windows) )
				throw new NotImplementedException("Non-windows OS's are not currently supported");

			SevenZipBase.SetLibraryPath( Path.Combine(Utility.GetResourcesDirectory(), "7z.dll") ); // Path to 7z.dll
			var extractor = new SevenZipExtractor(stream);
			extractor.ExtractArchive(destinationDirectory);
		}


		public static void OutputModTree([NotNull] DirectoryInfo directory, [NotNull] string outputPath)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof( directory ));
			if ( outputPath == null )
				throw new ArgumentNullException(nameof( outputPath ));

			Dictionary<string, object> root = GenerateArchiveTreeJson(directory);
			try
			{
				string json = JsonConvert.SerializeObject(
					root,
					Formatting.Indented,
					new JsonSerializerSettings
					{
						ContractResolver = new CamelCasePropertyNamesContractResolver(),
					}
				);

				File.WriteAllText(outputPath, json);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error writing output file '{outputPath}': {ex.Message}");
			}
		}

		[CanBeNull]
		public static Dictionary<string, object> GenerateArchiveTreeJson([NotNull] DirectoryInfo directory)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof( directory ));

			var root = new Dictionary<string, object>
			{
				{
					"Name", directory.Name
				},
				{
					"Type", "directory"
				},
				{
					"Contents", new List<object>()
				},
			};

			try
			{
				foreach ( FileInfo file in directory.EnumerateFilesSafely(searchPattern: "*.*") )
				{
					if ( file == null || !IsArchive(file.Extension) )
						continue;

					var fileInfo = new Dictionary<string, object>
					{
						{
							"Name", file.Name
						},
						{
							"Type", "file"
						},
					};
					List<ModDirectory.ArchiveEntry> archiveEntries = TraverseArchiveEntries(file.FullName);
					var archiveRoot = new Dictionary<string, object>
					{
						{
							"Name", file.Name
						},
						{
							"Type", "directory"
						},
						{
							"Contents", archiveEntries
						},
					};

					fileInfo["Contents"] = archiveRoot["Contents"];

					(root["Contents"] as List<object>)?.Add(fileInfo);
				}

				/*foreach (DirectoryInfo subdirectory in directory.EnumerateDirectoriesSafely())
                {
                    var subdirectoryInfo = new Dictionary<string, object>
                    {
                        { "Name", subdirectory.Name },
                        { "Type", "directory" },
                        { "Contents", GenerateArchiveTreeJson(subdirectory) }
                    };

                    (root["Contents"] as List<object>).Add(subdirectoryInfo);
                }*/
			}
			catch ( Exception ex )
			{
				Logger.Log($"Error generating archive tree for '{directory.FullName}': {ex.Message}");
				return null;
			}

			return root;
		}

		[NotNull]
		private static List<ModDirectory.ArchiveEntry> TraverseArchiveEntries([NotNull] string archivePath)
		{
			if ( archivePath == null )
				throw new ArgumentNullException(nameof( archivePath ));

			var archiveEntries = new List<ModDirectory.ArchiveEntry>();

			try
			{
				(IArchive archive, FileStream stream) = OpenArchive(archivePath);
				if ( archive is null || stream is null )
				{
					Logger.Log($"Unsupported archive format: '{Path.GetExtension(archivePath)}'");
					stream?.Dispose();
					return archiveEntries;
				}

				archiveEntries.AddRange(
					from entry in archive.Entries.Where(e => !e.IsDirectory)
					let pathParts = entry.Key.Split(
						archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase)
							? '\\' // Use backslash as separator for RAR files
							: '/'  // Use forward slash for other archive types
					)
					select new ModDirectory.ArchiveEntry
					{
						Name = pathParts[pathParts.Length - 1], Path = entry.Key,
					}
				);

				stream.Dispose();
			}
			catch ( Exception ex )
			{
				Logger.Log($"Error reading archive '{archivePath}': {ex.Message}");
			}

			return archiveEntries;
		}

		public static void ProcessArchiveEntry(
			[NotNull] IArchiveEntry entry,
			[NotNull] Dictionary<string, object> currentDirectory
		)
		{
			if ( entry == null )
				throw new ArgumentNullException(nameof( entry ));
			if ( currentDirectory == null )
				throw new ArgumentNullException(nameof( currentDirectory ));

			string[] pathParts = entry.Key.Split('/');
			bool isFile = !entry.IsDirectory;

			foreach ( string name in pathParts )
			{
				List<object> existingDirectory = currentDirectory["Contents"] as List<object>
					?? throw new InvalidDataException(
						$"Unexpected data type for directory contents: '{currentDirectory["Contents"]?.GetType()}'"
					);

				object existingChild = existingDirectory.Find(
					c => c is Dictionary<string, object> dict
						&& dict.ContainsKey("Name")
						&& dict["Name"] is string directoryName
						&& directoryName.Equals(name, StringComparison.OrdinalIgnoreCase)
				);

				if ( existingChild != null )
				{
					if ( isFile )
						((Dictionary<string, object>)existingChild)["Type"] = "file";

					currentDirectory = (Dictionary<string, object>)existingChild;
				}
				else
				{
					var child = new Dictionary<string, object>
					{
						{
							"Name", name
						},
						{
							"Type", isFile
								? "file"
								: "directory"
						},
						{
							"Contents", new List<object>()
						},
					};
					existingDirectory.Add(child);
					currentDirectory = child;
				}
			}
		}
	}
}
