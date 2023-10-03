// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemPathing;
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

		
		public class FileState
		{
			public string CurrentPath { get; set; }
			public string OriginalPath { get; set; }
			public bool IsVirtual { get; set; }  // False if it's from realFilesForCurrentGlob, True if it's from currentFilesState
		}

		public abstract class DeferredOperation
		{
			public Instruction MainInstruction { get; set; }
			public ComponentValidation MainComponentValidation { get; set; }
			public abstract bool Apply(HashSet<FileState> fileStates);
		}
		public class CopyOperation : DeferredOperation
		{
			public string SourcePath { get; set; }
			public string DestinationPath { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				FileState state = fileStates.FirstOrDefault(fs => fs.CurrentPath == SourcePath);
				return ( state != null && fileStates.Add(new FileState { CurrentPath = DestinationPath } ) )  || MainInstruction.Overwrite;
			}
		}

		public class FileExistsCheckOperation : DeferredOperation
		{
			public string FilePath { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				FileState state = fileStates.FirstOrDefault(fs => fs.CurrentPath == FilePath);
				return ( state != null && fileStates.Contains(state) ) || MainInstruction.Overwrite;
			}
		}

		public class DeleteOperation : DeferredOperation
		{
			public string FilePath { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				FileState state = fileStates.FirstOrDefault(fs => fs.CurrentPath == FilePath);
				return (state != null && fileStates.Remove(state)) || MainInstruction.Overwrite;
			}
		}
		
		public class RenameOperation : DeferredOperation
		{
			public string SourcePath { get; set; }
			public string NewName { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				bool success = true;
				foreach (FileState fileState in fileStates.Where(fs => PathHelper.WildcardPathMatch(fs.CurrentPath, SourcePath)).ToList())
				{
					string directoryPath = Path.GetDirectoryName(fileState.CurrentPath);
					if ( directoryPath is null )
					{
						MainComponentValidation.AddError($"fileState '{fileState.CurrentPath}' parent folder could not be determined.", MainInstruction);
						success = false;
						continue;
					}

					string newPath = PathHelper.FixPathFormatting(Path.Combine(directoryPath, NewName));
					fileState.CurrentPath = newPath;
				}

				return success;
			}
		}

		public class MoveOperation : DeferredOperation
		{
			public string SourcePath { get; set; }
			public string DestinationPath { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				bool success = true;
				FileState state = fileStates.FirstOrDefault(fs => fs.CurrentPath == SourcePath);
				if ( state == null )
					return false;

				success &= fileStates.Remove(state);
				state.CurrentPath = PathHelper.FixPathFormatting(Path.Combine(DestinationPath, Path.GetFileName(state.CurrentPath)));
				success &= fileStates.Add(state);
				
				return success;
			}
		}

		public class ExtractOperation : DeferredOperation
		{
			public string ArchivePath { get; set; }
			public string Destination { get; set; }

			public override bool Apply(HashSet<FileState> fileStates)
			{
				bool? success = null;
				foreach (string virtualFile in GetAbsolutePathsFromArchives(ArchivePath))
				{
					if ( success is null )
						success = true;

					string newPath = PathHelper.FixPathFormatting(virtualFile);
					success &= fileStates.Add(new FileState { CurrentPath = newPath, OriginalPath = newPath, IsVirtual = true });
				}

				return success is true;
			}

			// This method assumes the archive contents have been listed with their absolute extraction paths.
			private List<string> GetAbsolutePathsFromArchives([NotNull] string archivePath)
			{
				if (archivePath is null)
					throw new ArgumentNullException(nameof(archivePath));

				var extractedPaths = new List<string>();

				if (!ArchiveHelper.IsArchive(archivePath))
				{
					MainComponentValidation.AddError($"'{archivePath}' is not an archive.", MainInstruction);
					return new List<string>(); // skip non-archives
				}

				// If the archive is a self-extracting executable, we can't accurately predict its internal structure
				if (Path.GetExtension(archivePath) == ".exe")
				{
					MainComponentValidation.AddWarning($"'{archivePath}' is an EXE file, assuming self extracting executable?", MainInstruction);
					return new List<string>();
				}

				using (FileStream stream = File.OpenRead(archivePath))
				{
					IArchive archive = null;

					if (archivePath.EndsWith(value: ".zip", StringComparison.OrdinalIgnoreCase))
					{
						archive = ZipArchive.Open(stream);
					}
					else if (archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase))
					{
						archive = RarArchive.Open(stream);
					}
					else if (archivePath.EndsWith(value: ".7z", StringComparison.OrdinalIgnoreCase))
					{
						archive = SevenZipArchive.Open(stream);
					}

					if (archive is null)
					{
						MainComponentValidation.AddError($"Archive file '{archivePath}' could not be opened.", MainInstruction);
						return new List<string>(); // could not open this archive, continue with the next
					}

					string archiveParentDir = Path.GetDirectoryName(archivePath);
					if ( archiveParentDir is null )
					{
						MainComponentValidation.AddError($"Archive file '{archivePath}' does not have a parent folder ergo not a valid file.", MainInstruction);
						return new List<string>();
					}

					string archiveDirectory = Path.Combine(archiveParentDir, Path.GetFileNameWithoutExtension(archivePath));

					extractedPaths.AddRange(from entry in archive.Entries where !entry.IsDirectory select PathHelper.FixPathFormatting(Path.Combine(archiveDirectory, entry.Key)));
				}

				return extractedPaths;
			}
		}

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
								$"Missing Required Archives for 'Extract' action: [{string.Join(separator: ",", instruction.Source)}]",
								instruction
							);
							success = false;
						}
					}

					return success;
				}

				var dryRunInstructions = new List<Instruction>(ComponentToValidate.Instructions);

				int index = 0;
				while (index < dryRunInstructions.Count)
				{
					Instruction instruction = dryRunInstructions[index];
    
					if (instruction.Action == Instruction.ActionType.Choose)
					{
						var instructionsToAdd = new List<Instruction>();
        
						foreach (Option thisOption in ComponentToValidate.Options)
						{
							if (thisOption == null)
								continue;

							if (instruction.Source.Contains(thisOption.Guid.ToString()))
							{
								// Add thisOption's instructions to the list of instructions to be added.
								instructionsToAdd.AddRange(thisOption.Instructions);
							}
						}

						if (instructionsToAdd.Count > 0)
						{
							// If we found instructions to add, remove the original and insert the new ones.
							dryRunInstructions.RemoveAt(index);
							dryRunInstructions.InsertRange(index, instructionsToAdd);
            
							// Adjust index to skip past the newly added instructions.
							index += instructionsToAdd.Count;
							continue;
						}
					}
    
					index++;
				}

				// Main Logic
				var fileStates = new HashSet<FileState>();

				foreach ( string glob in ComponentToValidate.Instructions.SelectMany(
						i => i.Source.ConvertAll(Utility.Utility.ReplaceCustomVariables)
					) )
				{
					fileStates.UnionWith(
						PathHelper.EnumerateFilesWithWildcards(new List<string> { glob }).Select(
							realFile => new FileState
							{
								CurrentPath = realFile, OriginalPath = realFile, IsVirtual = false,
							}
						)
					);
				}

				foreach ( Instruction instruction in dryRunInstructions )
				{
					var deferredOperations = new List<DeferredOperation>();
					List<string> sourceGlobs = instruction.Source.ConvertAll(Utility.Utility.ReplaceCustomVariables);
					string destination = Utility.Utility.ReplaceCustomVariables(instruction.Destination);

					foreach ( string sourceGlob in sourceGlobs )
					{
						List<FileState> sourceMatches;
						switch ( instruction.Action )
						{
							case Instruction.ActionType.Extract:
								sourceMatches = fileStates.Where(
									fs => PathHelper.WildcardPathMatch(fs.CurrentPath, sourceGlob)
								).ToList();

								foreach ( FileState matchedState in sourceMatches )
								{
									if ( matchedState.IsVirtual )
									{
										deferredOperations.Add(
											new ExtractOperation
											{
												ArchivePath = matchedState.CurrentPath, Destination = destination,
											}
										);
									}
									else
									{
										List<string> potentialRealArchives = PathHelper.EnumerateFilesWithWildcards(
											new List<string> { matchedState.CurrentPath, }
										);
										foreach ( string potentialArchive in potentialRealArchives )
										{
											deferredOperations.Add(
												new ExtractOperation
												{
													ArchivePath = potentialArchive,
													Destination = destination,
													MainInstruction=instruction,
													MainComponentValidation = this,
												}
											);
										}
									}
								}

								break;

							case Instruction.ActionType.Move:
							case Instruction.ActionType.Rename:
							case Instruction.ActionType.Copy:
							case Instruction.ActionType.Delete:
							case Instruction.ActionType.Run:
							case Instruction.ActionType.Execute:
							case Instruction.ActionType.HoloPatcher:
							case Instruction.ActionType.TSLPatcher:
								sourceMatches = fileStates.Where(
									fs => PathHelper.WildcardPathMatch(fs.CurrentPath, sourceGlob)
								).ToList();
								foreach ( FileState matchedState in sourceMatches )
								{
									string newPath = Path.Combine(
										destination,
										Path.GetFileName(matchedState.CurrentPath)
									);

									// ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
									switch ( instruction.Action )
									{
										case Instruction.ActionType.Move:
											deferredOperations.Add(
												new MoveOperation
												{
													SourcePath = matchedState.CurrentPath, DestinationPath = newPath, MainInstruction=instruction, MainComponentValidation = this,
												}
											);
											break;
										case Instruction.ActionType.Rename:
											deferredOperations.Add(
												new RenameOperation
												{
													SourcePath = matchedState.CurrentPath, NewName = newPath, MainInstruction=instruction, MainComponentValidation = this,
												}
											);
											break;
										case Instruction.ActionType.Copy:
											deferredOperations.Add(
												new CopyOperation
												{
													SourcePath = matchedState.CurrentPath, DestinationPath = newPath, MainInstruction=instruction, MainComponentValidation = this,
												}
											);
											break;
										case Instruction.ActionType.Delete:
											deferredOperations.Add(
												new DeleteOperation
												{
													FilePath = matchedState.CurrentPath, MainInstruction=instruction, MainComponentValidation = this,
												}
											);
											break;
										case Instruction.ActionType.Run:
										case Instruction.ActionType.Execute:
										case Instruction.ActionType.HoloPatcher:
										case Instruction.ActionType.TSLPatcher:
											deferredOperations.Add(
												new FileExistsCheckOperation
												{
													FilePath = matchedState.CurrentPath, MainInstruction=instruction, MainComponentValidation = this,
												}
											);
											break;
									}
								}

								break;

							//... any other actions ...

							case Instruction.ActionType.DelDuplicate:
							case Instruction.ActionType.Choose:
							case Instruction.ActionType.Unset:
							default:
								// Optional: Error handling for unsupported operations
								break;
						}
					}

					// Apply deferred operations
					foreach (DeferredOperation operation in deferredOperations)
					{
						success &= operation.Apply(fileStates);
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

				foreach ( Instruction optionInstruction in thisOption.Instructions )
				{
					instructions.Add(optionInstruction);
				}
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
