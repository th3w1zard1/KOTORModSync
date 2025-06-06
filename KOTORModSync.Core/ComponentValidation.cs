﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;

namespace KOTORModSync.Core
{
	public sealed class ComponentValidation
	{
		public enum ArchivePathCode
		{
			NotAnArchive,
			PathMissingArchiveName,
			CouldNotOpenArchive,
			NotFoundInArchive,
			FoundSuccessfully,
			NeedsAppendedArchiveName,
			NoArchivesFound,
		}

		[CanBeNull] private readonly List<Component> _componentsList;

		[NotNull] private readonly List<ValidationResult> _validationResults = new List<ValidationResult>();
		[NotNull] public readonly Component ComponentToValidate;

		public ComponentValidation([NotNull] Component component, [CanBeNull] List<Component> componentsList = null)
		{
			ComponentToValidate = component ?? throw new ArgumentNullException(nameof( component ));
			if ( componentsList is null )
				return;

			_componentsList = new List<Component>(componentsList);
		}

		public bool Run() =>
			// Verify all the instructions' paths line up with hierarchy of the archives
			VerifyExtractPaths()
			// Ensure all the 'Destination' keys are valid for their respective action.
			&& ParseDestinationWithAction();

		private void AddError([NotNull] string message, [NotNull] Instruction instruction) =>
			_validationResults.Add(new ValidationResult(this, instruction, message, isError: true));

		private void AddWarning([NotNull] string message, [NotNull] Instruction instruction) =>
			_validationResults.Add(new ValidationResult(this, instruction, message, isError: false));

		[NotNull]
		public List<string> GetErrors() =>
			_validationResults.Where(r => r.IsError).Select(r => r.Message).ToList();

		[NotNull]
		public List<string> GetErrors(int instructionIndex) =>
			_validationResults.Where(r => r.InstructionIndex == instructionIndex && r.IsError).Select(r => r.Message)
				.ToList();

		[NotNull]
		public List<string> GetErrors([CanBeNull] Instruction instruction) =>
			_validationResults.Where(r => r.Instruction == instruction && r.IsError).Select(r => r.Message).ToList();

		[NotNull]
		public List<string> GetWarnings() =>
			_validationResults.Where(r => !r.IsError).Select(r => r.Message).ToList();

		[NotNull]
		public List<string> GetWarnings(int instructionIndex) =>
			_validationResults.Where(r => r.InstructionIndex == instructionIndex && !r.IsError).Select(r => r.Message)
				.ToList();

		[NotNull]
		public List<string> GetWarnings([CanBeNull] Instruction instruction) =>
			_validationResults.Where(r => r.Instruction == instruction && !r.IsError).Select(r => r.Message).ToList();

		private bool VerifyExtractPaths()
		{
			try
			{
				bool success = true;

				// Confirm that all Dependencies are found in either InstallBefore and InstallAfter:
				List<string> allArchives = GetAllArchivesFromInstructions();

				// probably something wrong if there's no archives found.
				if ( allArchives.IsNullOrEmptyCollection() )
				{
					foreach ( Instruction instruction in ComponentToValidate.Instructions )
					{
						if ( instruction.Action == Instruction.ActionType.Extract )
						{
							AddError(
								$"Missing Required Archives: [{string.Join(separator: ",", instruction.Source)}]",
								instruction
							);
							success = false;
						}
					}

					return success;
				}

				var instructions = ComponentToValidate.Instructions.ToList();
				foreach ( Option thisOption in ComponentToValidate.Options )
				{
					if ( thisOption is null )
						continue;

					instructions.AddRange(thisOption.Instructions);
				}

				foreach ( Instruction instruction in instructions )
				{
					switch ( instruction.Action )
					{
						case Instruction.ActionType.Unset:
							AddError(message: "Action cannot be null", instruction);
							success = false;
							continue;
						// we already checked if the archive exists in GetAllArchivesFromInstructions.
						case Instruction.ActionType.Extract:
						// 'choose' action uses Source as list of guids to options.
						case Instruction.ActionType.Choose:
							continue;
						case Instruction.ActionType.Execute:
							break;
						case Instruction.ActionType.TSLPatcher:
							break;
						case Instruction.ActionType.Move:
							break;
						case Instruction.ActionType.Copy:
							break;
						case Instruction.ActionType.Rename:
							break;
						case Instruction.ActionType.Delete:
							break;
						case Instruction.ActionType.DelDuplicate:
							break;
						case Instruction.ActionType.HoloPatcher:
							break;
						case Instruction.ActionType.Run:
							break;
						default:
							break;
					}

					// ReSharper disable once ConditionIsAlwaysTrueOrFalse
					if ( instruction.Source is null )
						// ReSharper disable twice HeuristicUnreachableCode
					{
						AddWarning(message: "Instruction does not have a 'Source' key defined", instruction);
						success = false;
						continue;
					}

					bool archiveNameFound = true;
					for ( int index = 0; index < instruction.Source.Count; index++ )
					{
						string sourcePath = PathHelper.FixPathFormatting(instruction.Source[index]);

						// todo
						if ( sourcePath.StartsWith(value: "<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase) )
							continue;

						// ensure tslpatcher.exe sourcePaths use the action 'tslpatcher'
						if ( sourcePath.EndsWith(value: "tslpatcher.exe", StringComparison.OrdinalIgnoreCase)
							&& instruction.Action != Instruction.ActionType.TSLPatcher )
						{
							AddWarning(
								message:
								"'tslpatcher.exe' used in Source path without the action 'tslpatcher', was this intentional?",
								instruction
							);
						}

						(bool, bool) result = IsSourcePathInArchives(sourcePath, allArchives, instruction);

						// For some unholy reason, some archives act like there's another top level folder named after the archive to extract.
						// doesn't even seem to be related to the archive type. Can't reproduce in 7zip either.
						// either way, this will hide the issue until a real solution comes along.
						if ( !result.Item1 && MainConfig.AttemptFixes )
						{
							// Split the directory name using the directory separator character
							string[] parts = sourcePath.Split(Path.DirectorySeparatorChar);

							// Add the first part of the path and repeat it at the beginning
							// i.e. archive/my/custom/path becomes archive/archive/my/custom/path
							string duplicatedPart = parts[1] + Path.DirectorySeparatorChar + parts[1];
							string[] remainingParts = parts.Skip(2).ToArray();

							string path = string.Join(
								Path.DirectorySeparatorChar.ToString(),
								new[]
								{
									parts[0], duplicatedPart,
								}.Concat(remainingParts)
							);

							result = IsSourcePathInArchives(path, allArchives, instruction);
							if ( result.Item1 )
							{
								_ = Logger.LogAsync("Fixing the above issue automatically...");
								instruction.Source[index] = path;
							}
						}

						success &= result.Item1;
						archiveNameFound &= result.Item2;
					}

					if ( !archiveNameFound )
					{
						AddWarning(
							"'Source' path does not include the archive's name as part"
							+ " of the extraction folder, possible FileNotFound exception.",
							instruction
						);
					}
				}

				return success;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return false;
			}
		}

		[NotNull]
		public List<string> GetAllArchivesFromInstructions()
		{
			var allArchives = new List<string>();

			var instructions = ComponentToValidate.Instructions.ToList();
			foreach ( Option thisOption in ComponentToValidate.Options )
			{
				if ( thisOption is null )
					continue;

				instructions.AddRange(thisOption.Instructions);
			}

			foreach ( Instruction instruction in instructions )
			{
				if ( !(_componentsList is null) && !Component.ShouldRunInstruction(instruction, _componentsList) )
					continue;
				if ( instruction.Action != Instruction.ActionType.Extract )
					continue;

				List<string> realPaths = PathHelper.EnumerateFilesWithWildcards(
					instruction.Source.ConvertAll(Utility.Utility.ReplaceCustomVariables),
					includeSubFolders: false
				);
				if ( realPaths is null )
				{
					AddError(message: "Could not find real paths", instruction);
					continue;
				}

				foreach ( string realSourcePath in realPaths )
				{
					if ( Path.GetExtension(realSourcePath).Equals(value: ".exe", StringComparison.OrdinalIgnoreCase) )
					{
						allArchives.Add(realSourcePath);
						continue; // no easy way to verify self-extracting executables ( see ArchiveHelper.IsValidArchive )
					}

					if ( File.Exists(realSourcePath) )
					{
						allArchives.Add(realSourcePath);
						continue;
					}

					AddError("Missing required download:" + $" '{Path.GetFileName(realSourcePath)}'", instruction);
				}
			}

			return allArchives;
		}

		private bool ParseDestinationWithAction()
		{
			bool success = true;
			var instructions = ComponentToValidate.Instructions.ToList();
			foreach ( Option thisOption in ComponentToValidate.Options )
			{
				if ( thisOption is null )
					continue;

				instructions.AddRange(thisOption.Instructions);
			}

			foreach ( Instruction instruction in instructions )
			{
				switch ( instruction.Action )
				{
					case Instruction.ActionType.Unset:
						continue;
					// tslpatcher must always use <<kotorDirectory>> and nothing else.
					case Instruction.ActionType.TSLPatcher when string.IsNullOrEmpty(instruction.Destination):
						AddWarning(
							message:
							"Destination must be <<kotorDirectory>> with 'TSLPatcher' action, setting it now automatically.",
							instruction
						);
						instruction.Destination = "<<kotorDirectory>>";
						break;

					case Instruction.ActionType.TSLPatcher when !instruction.Destination.Equals(
						value: "<<kotorDirectory>>",
						StringComparison.OrdinalIgnoreCase
					):
						success = false;
						AddError(
							"'Destination' key must be either null or string literal '<<kotorDirectory>>'"
							+ $" for this action. Got '{instruction.Destination}'",
							instruction
						);
						if ( MainConfig.AttemptFixes )
						{
							Logger.Log("Fixing the above issue automatically.");
							instruction.Destination = "<<kotorDirectory>>";
							success = true;
						}

						break;
					// choose, extract, and delete cannot use the 'Destination' key.
					case Instruction.ActionType.Choose:
					case Instruction.ActionType.Extract:
					case Instruction.ActionType.Delete:
						if ( string.IsNullOrEmpty(instruction.Destination) )
							break;

						success = false;
						AddError(
							$"'Destination' key cannot be used with this action. Got '{instruction.Destination}'",
							instruction
						);

						if ( MainConfig.AttemptFixes )
						{
							Logger.Log("Fixing the above issue automatically.");
							instruction.Destination = string.Empty;
							success = true;
						}

						break;
					// rename should never use <<kotorDirectory>>\\Override
					case Instruction.ActionType.Rename:
						if ( instruction.Destination.Equals(
								$"<<kotorDirectory>>{Path.DirectorySeparatorChar}Override",
								StringComparison.OrdinalIgnoreCase
							) )
						{
							success = false;
							AddError(
								"Incorrect 'Destination' format."
								+ $" Got '{instruction.Destination}',"
								+ " expected a filename.",
								instruction
							);
						}

						break;
					// don't validate arduous execute/run actions.
					case Instruction.ActionType.Run:
					case Instruction.ActionType.Execute:
						break;
					case Instruction.ActionType.Move:
					case Instruction.ActionType.Copy:
					case Instruction.ActionType.DelDuplicate:
					case Instruction.ActionType.HoloPatcher:
					default:
						string destinationPath = null;
						if ( !string.IsNullOrEmpty(instruction.Destination) )
						{
							destinationPath = PathHelper.FixPathFormatting(
								Utility.Utility.ReplaceCustomVariables(instruction.Destination)
							);
							if ( MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationPath) )
								destinationPath = PathHelper.GetCaseSensitivePath(destinationPath).Item1;
						}

						if ( string.IsNullOrWhiteSpace(destinationPath)
							|| !PathValidator.IsValidPath(destinationPath)
							|| !Directory.Exists(destinationPath) )
						{
							success = false;
							AddError($"Destination cannot be found! Got '{destinationPath}'", instruction);
							if ( MainConfig.AttemptFixes && PathValidator.IsValidPath(destinationPath) )
							{
								Logger.Log("Fixing the above error automatically...");
								try
								{
									// ReSharper disable once AssignNullToNotNullAttribute
									_ = Directory.CreateDirectory(destinationPath);
									success = true;
								}
								catch ( Exception e )
								{
									Logger.LogException(e);
									AddError(e.Message, instruction);
									success = false;
								}
							}
						}

						break;
				}
			}

			return success;
		}

		[NotNull]
		private static string GetErrorDescription(ArchivePathCode code)
		{
			switch ( code )
			{
				case ArchivePathCode.FoundSuccessfully:
					return "File successfully found in archive.";
				case ArchivePathCode.NotAnArchive:
					return "Not an archive";
				case ArchivePathCode.PathMissingArchiveName:
					return "Missing archive name in path";
				case ArchivePathCode.CouldNotOpenArchive:
					return "Could not open archive";
				case ArchivePathCode.NotFoundInArchive:
					return "Not found in archive";
				case ArchivePathCode.NoArchivesFound:
					return "No archives found/no extract instructions created";
				case ArchivePathCode.NeedsAppendedArchiveName:
				default:
					return "Unknown error";
			}
		}

		private (bool, bool) IsSourcePathInArchives(
			[NotNull] string sourcePath,
			[NotNull] List<string> allArchives,
			[NotNull] Instruction instruction
		)
		{
			if ( sourcePath is null )
				throw new ArgumentNullException(nameof( sourcePath ));
			if ( allArchives is null )
				throw new ArgumentNullException(nameof( allArchives ));
			if ( instruction is null )
				throw new ArgumentNullException(nameof( instruction ));

			bool foundInAnyArchive = false;
			bool hasError = false;
			bool archiveNameFound = false;
			string errorDescription = string.Empty;

			sourcePath = PathHelper.FixPathFormatting(sourcePath)
				.Replace($"<<modDirectory>>{Path.DirectorySeparatorChar}", newValue: "").Replace(
					$"<<kotorDirectory>>{Path.DirectorySeparatorChar}",
					newValue: ""
				);

			foreach ( string archivePath in allArchives )
			{
				if ( archivePath is null )
				{
					AddError(message: "Archive is not a valid file path", instruction);
					continue;
				}

				// Check if the archive name matches the first portion of the sourcePath
				string archiveName = Path.GetFileNameWithoutExtension(archivePath);

				string[] pathParts = sourcePath.Split(Path.DirectorySeparatorChar);

				archiveNameFound = PathHelper.WildcardPathMatch(archiveName, pathParts[0]);
				ArchivePathCode code = IsPathInArchive(sourcePath, archivePath);

				if ( code == ArchivePathCode.FoundSuccessfully )
				{
					foundInAnyArchive = true;
					break;
				}

				if ( code == ArchivePathCode.NotFoundInArchive )
				{
					continue;
				}

				hasError = true;
				errorDescription += GetErrorDescription(code) + Environment.NewLine;
			}

			if ( hasError )
			{
				AddError($"Invalid source path '{sourcePath}'. Reason: {errorDescription}", instruction);
				return (false, archiveNameFound);
			}

			if ( _componentsList is null )
			{
				AddError(new NullReferenceException(nameof( _componentsList )).Message, instruction);
				return (false, archiveNameFound);
			}

			if ( foundInAnyArchive || !Component.ShouldRunInstruction(instruction, _componentsList) )
			{
				return (true, true);
			}

			// todo, stop displaying errors for self extracting executables. This is the only mod using one that I've seen out of 200-some.
			if ( ComponentToValidate.Name.Equals(value: "Improved AI", StringComparison.OrdinalIgnoreCase) )
			{
				return (true, true);
			}

			// archive not required if instruction isn't running.
			if ( !Component.ShouldRunInstruction(instruction, _componentsList) )
			{
				return (true, true);
			}

			AddError($"Failed to find '{sourcePath}' in any archives!", instruction);
			return (false, archiveNameFound);
		}

		private static ArchivePathCode IsPathInArchive([NotNull] string relativePath, [NotNull] string archivePath)
		{
			if ( relativePath is null )
				throw new ArgumentNullException(nameof( relativePath ));
			if ( archivePath is null )
				throw new ArgumentNullException(nameof( archivePath ));

			if ( !ArchiveHelper.IsArchive(archivePath) )
				return ArchivePathCode.NotAnArchive;

			// todo: self-extracting 7z executables
			if ( Path.GetExtension(archivePath) == ".exe" )
				return ArchivePathCode.FoundSuccessfully;

			using ( FileStream stream = File.OpenRead(archivePath) )
			{
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

				if ( archive is null )
					return ArchivePathCode.CouldNotOpenArchive;

				// everything is extracted to a new directory named after the archive.
				string archiveNameAppend = Path.GetFileNameWithoutExtension(archivePath);

				// if the Source key represents the top level extraction directory, check that first.
				if ( PathHelper.WildcardPathMatch(archiveNameAppend, relativePath) )
					return ArchivePathCode.FoundSuccessfully;

				var folderPaths = new HashSet<string>();

				foreach ( IArchiveEntry entry in archive.Entries )
				{
					// Append extracted directory and ensure every slash is a backslash.
					string itemInArchivePath = archiveNameAppend
						+ Path.DirectorySeparatorChar
						+ entry.Key.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

					// Some archives loop through folders while others don't.
					string folderName = Path.GetFileName(itemInArchivePath);
					if ( entry.IsDirectory )
					{
						folderName = Path.GetDirectoryName(itemInArchivePath);
					}

					// Add the folder path to the list, after removing trailing slashes.
					if ( !string.IsNullOrEmpty(folderName) )
					{
						_ = folderPaths.Add(
							folderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
						);
					}

					// Check if itemInArchivePath matches relativePath using wildcard matching.
					if ( PathHelper.WildcardPathMatch(itemInArchivePath, relativePath) )
						return ArchivePathCode.FoundSuccessfully;
				}

				// check if instruction.Source matches a folder.
				foreach ( string folderPath in folderPaths )
				{
					if ( !(folderPath is null) && PathHelper.WildcardPathMatch(folderPath, relativePath) )
						return ArchivePathCode.FoundSuccessfully;
				}
			}

			return ArchivePathCode.NotFoundInArchive;
		}
	}
}
