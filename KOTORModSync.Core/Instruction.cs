// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Data;
using KOTORModSync.Core.FileSystemPathing;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace KOTORModSync.Core
{
	public sealed class Instruction : INotifyPropertyChanged
	{
		public enum ActionExitCode
		{
			UnauthorizedAccessException = -1,
			Success,
			InvalidSelfExtractingExecutable,
			InvalidArchive,
			ArchiveParseError,
			FileNotFoundPost,
			IOException,
			RenameTargetAlreadyExists,
			TSLPatcherCLIError,
			ChildProcessError,
			UnknownError,
			UnknownInnerError,
			TSLPatcherError,
			UnknownInstruction,
			TSLPatcherLogNotFound,
			FallbackArchiveExtractionFailed,
		}

		public enum ActionType
		{
			Extract,
			Execute,
			TSLPatcher,
			Move,
			Copy,
			Rename,
			Delete,
			DelDuplicate,
			Choose,
			HoloPatcher,
			Run,
			Unset,
		}


		private ActionType _action;

		[NotNull] private string _arguments = string.Empty;

		[NotNull] private List<Guid> _dependencies = new List<Guid>();

		[NotNull] private string _destination = string.Empty;

		private bool _overwrite;

		[NotNull] private List<Guid> _restrictions = new List<Guid>();

		[NotNull][ItemNotNull] private List<string> _source = new List<string>();

		public static IEnumerable<string> ActionTypes => Enum.GetValues(typeof( ActionType )).Cast<ActionType>()
			.Select(actionType => actionType.ToString());

		[JsonIgnore]
		public ActionType Action
		{
			get => _action;
			set
			{
				_action = value;
				OnPropertyChanged();
			}
		}

		[JsonProperty(nameof( Action ))]
		public string ActionString
		{
			get => Action.ToString();
			set => Action = (ActionType)Enum.Parse(typeof( ActionType ), value);
		}

		[NotNull][ItemNotNull] public List<string> Source
		{
			get => _source;
			set
			{
				_source = value;
				OnPropertyChanged();
			}
		}

		[NotNull] public string Destination
		{
			get => _destination;
			set
			{
				_destination = value;
				OnPropertyChanged();
			}
		}

		public bool Overwrite
		{
			get => _overwrite;
			set
			{
				_overwrite = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Arguments
		{
			get => _arguments;
			set
			{
				_arguments = value;
				OnPropertyChanged();
			}
		}

		[NotNull] public List<Guid> Dependencies
		{
			get => _dependencies;
			set
			{
				_dependencies = value;
				OnPropertyChanged();
			}
		}

		[NotNull] public List<Guid> Restrictions
		{
			get => _restrictions;
			set
			{
				_restrictions = value;
				OnPropertyChanged();
			}
		}

		[NotNull][ItemNotNull] private List<string> RealSourcePaths { get; set; } = new List<string>();
		[CanBeNull] private DirectoryInfo RealDestinationPath { get; set; }

		private Component _parentComponent { get; set; }

		public Dictionary<FileInfo, SHA1> ExpectedChecksums { get; set; }
		public Dictionary<FileInfo, SHA1> OriginalChecksums { get; internal set; }

		public Component GetParentComponent() => _parentComponent;
		public void SetParentComponent(Component thisComponent) => _parentComponent = thisComponent;

		public static async Task<bool> ExecuteInstructionAsync([NotNull] Func<Task<bool>> instructionMethod) =>
			await (instructionMethod() ?? throw new ArgumentNullException(nameof( instructionMethod ))).ConfigureAwait(
				false
			);

		// This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
		// This method should not be ran before an instruction is executed.
		// Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
		// ^ perhaps the above is user error though? User should check what they are running in advance perhaps? Either way, we attempt to baby them here.
		// noValidate: Don't validate if the path/file exists on disk
		// noParse: Don't perform any manipulations on Source or Destination, besides the aforementioned ReplaceCustomVariables.
		internal void SetRealPaths(bool noParse = false, bool noValidate = false)
		{
			// Get real path then enumerate the files/folders with wildcards and add them to the list
			if ( Source is null )
				throw new NullReferenceException(nameof( Source ));

			
			List<string> newSourcePaths = Source.ConvertAll(Utility.Utility.ReplaceCustomVariables);
			if ( !noParse )
			{
				newSourcePaths = PathHelper.EnumerateFilesWithWildcards(newSourcePaths);

				if ( !noValidate )
				{
					if ( newSourcePaths.IsNullOrEmptyOrAllNull() )
						throw new NullReferenceException(nameof( newSourcePaths ));

					if ( newSourcePaths.Any(f => !File.Exists(f)) )
					{
						throw new FileNotFoundException(
							$"Could not find all files in the 'Source' path on disk! Got [{string.Join(separator: ", ", Source)}]"
						);
					}
				}

				// Remove duplicates
				RealSourcePaths = (
					MainConfig.CaseInsensitivePathing
						? newSourcePaths.Distinct(StringComparer.OrdinalIgnoreCase)
						: newSourcePaths.Distinct()
				).ToList();
			}

			string destinationPath = Utility.Utility.ReplaceCustomVariables(Destination);
			DirectoryInfo thisDestination = PathHelper.TryGetValidDirectoryInfo(destinationPath);
			if ( noParse )
			{
				RealDestinationPath = thisDestination;
				return;
			}

			if ( !noValidate && !thisDestination?.Exists == true )
			{
				if ( MainConfig.CaseInsensitivePathing )
				{
					thisDestination = PathHelper.GetCaseSensitivePath(thisDestination);
				}

				if ( !noValidate && !thisDestination?.Exists == true )
					throw new DirectoryNotFoundException("Could not find the 'Destination' path on disk!");
			}

			RealDestinationPath = thisDestination;
		}

		// ReSharper disable once AssignNullToNotNullAttribute
		public async Task<ActionExitCode> ExtractFileAsync(
			DirectoryInfo argDestinationPath = null,
			[NotNull][ItemNotNull] List<string> argSourcePaths = null
		)
		{
			try
			{
				RealSourcePaths = argSourcePaths
					?? RealSourcePaths ?? throw new ArgumentNullException(nameof( argSourcePaths ));

				ActionExitCode exitCode = ActionExitCode.Success;
				int maxCount = MainConfig.UseMultiThreadedIO
					? 16
					: 1;
				using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
				{
					try
					{
						using ( var cts = new CancellationTokenSource() )
						{
							try
							{
								var extractionTasks = RealSourcePaths
									.Select(sourcePath => InnerExtractFileAsync(sourcePath, cts.Token)).ToList();

								await Task.WhenAll(extractionTasks); // Wait for all extraction tasks to complete
							}
							catch ( IndexOutOfRangeException )
							{
								await Logger.LogWarningAsync(
									"Falling back to 7-Zip and restarting entire archive extraction due to the above error."
								);
								cts.Cancel();
								cts.Token.ThrowIfCancellationRequested();
							}
							catch ( OperationCanceledException ex )
							{
								await Logger.LogWarningAsync(ex.Message);
								cts.Cancel();
								cts.Token.ThrowIfCancellationRequested();
							}
							catch ( IOException ex )
							{
								await Logger.LogExceptionAsync(ex);
								cts.Cancel();
								if ( exitCode == ActionExitCode.Success )
								{
									exitCode = ActionExitCode.IOException;
								}
							}
							catch ( Exception ex )
							{
								await Logger.LogExceptionAsync(ex);
								cts.Cancel();
								if ( exitCode == ActionExitCode.Success )
								{
									exitCode = ActionExitCode.UnknownError;
								}
							}
						}
					}
					catch ( OperationCanceledException )
					{
						// Restarting all tasks using ArchiveHelper.ExtractWith7Zip
						try
						{
							await Logger.LogAsync("Starting 7z.dll fallback extraction...");
							foreach ( string archivePath in RealSourcePaths )
							{
								var thisArchive = new FileInfo(archivePath);
								using ( FileStream stream = File.OpenRead(archivePath) )
								{
									string destinationDirectory = Path.Combine(
										argDestinationPath?.FullName ?? archivePath,
										Path.GetFileNameWithoutExtension(thisArchive.Name)
									);
									if ( MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationDirectory) )
									{
										destinationDirectory = PathHelper.GetCaseSensitivePath(
											destinationDirectory,
											isFile: false
										).Item1;
									}

									string destinationRelDirPath = MainConfig.DestinationPath is null
										? destinationDirectory
										: PathHelper.GetRelativePath(
											MainConfig.DestinationPath.FullName,
											destinationDirectory
										);
									if ( !Directory.Exists(destinationDirectory) )
									{
										_ = Logger.LogAsync($"Create directory '{destinationRelDirPath}'");
										_ = Directory.CreateDirectory(destinationDirectory);
									}

									_ = Logger.LogAsync(
										$"Fallback extraction of '{thisArchive.Name}' to '{destinationRelDirPath}'"
									);
									ArchiveHelper.ExtractWith7Zip(stream, destinationDirectory);
									_ = Logger.LogAsync($"Fallback extraction of '{thisArchive.Name}' completed.");
								}
							}
						}
						catch ( Exception ex )
						{
							await Logger.LogExceptionAsync(ex);
							if ( exitCode == ActionExitCode.Success )
								exitCode = ActionExitCode.FallbackArchiveExtractionFailed;
						}
					}

					return exitCode; // Extraction succeeded

					async Task InnerExtractFileAsync(string sourcePath, CancellationToken cancellationToken)
					{
						if ( cancellationToken.IsCancellationRequested )
							return;

						await semaphore.WaitAsync(cancellationToken); // Wait for a semaphore slot

						try
						{
							var thisArchive = new FileInfo(sourcePath);
							string sourceRelDirPath = MainConfig.SourcePath is null
								? sourcePath
								: PathHelper.GetRelativePath(
									MainConfig.SourcePath.FullName,
									sourcePath
								);
							argDestinationPath = argDestinationPath
								?? thisArchive.Directory
								?? throw new ArgumentNullException(nameof( argDestinationPath ));

							await Logger.LogAsync($"Using archive path: '{sourceRelDirPath}'");

							// (attempt to) handle self-extracting executable archives (7zip)
							if ( thisArchive.Extension.Equals(value: ".exe", StringComparison.OrdinalIgnoreCase) )
							{
								(int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
									thisArchive.FullName,
									$" -o\"{thisArchive.DirectoryName}\" -y",
									noAdmin: MainConfig.NoAdmin
								);

								if ( result.Item1 == 0 )
									return;

								exitCode = ActionExitCode.InvalidSelfExtractingExecutable;
								throw new InvalidOperationException(
									$"'{sourceRelDirPath}' is not a self-extracting executable as previously assumed. Cannot extract."
								);
							}

							using ( FileStream stream = File.OpenRead(thisArchive.FullName) )
							{
								IArchive archive;

								switch ( thisArchive.Extension.ToLowerInvariant() )
								{
									case ".zip":
										archive = ZipArchive.Open(stream);
										break;
									case ".rar":
										archive = RarArchive.Open(stream);
										break;
									case ".7z":
										archive = SevenZipArchive.Open(stream);
										break;
									default:
										archive = ArchiveFactory.Open(stream);
										break;
								}

								using ( archive )
								using ( IReader reader = archive.ExtractAllEntries() )
								{
									while ( reader.MoveToNextEntry() )
									{
										if ( reader.Entry.IsDirectory )
											continue;

										string extractFolderName = Path.GetFileNameWithoutExtension(thisArchive.Name);
										string destinationItemPath = Path.Combine(
											argDestinationPath.FullName,
											extractFolderName,
											reader.Entry.Key
										);
										string destinationDirectory = Path.GetDirectoryName(destinationItemPath)
											?? throw new NullReferenceException($"Path.GetDirectoryName({destinationItemPath})");
										if ( MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationDirectory) )
										{
											destinationDirectory = PathHelper.GetCaseSensitivePath(
												destinationDirectory,
												isFile: false
											).Item1;
										}
									
										string destinationRelDirPath = MainConfig.SourcePath is null
											? destinationDirectory
											: PathHelper.GetRelativePath(
												MainConfig.SourcePath.FullName,
												destinationDirectory
											);

										if ( !Directory.Exists(destinationDirectory) )
										{
											await Logger.LogAsync($"Create directory '{destinationRelDirPath}'");
											_ = Directory.CreateDirectory(destinationDirectory);
										}

										await Logger.LogAsync($"Extract '{reader.Entry.Key}' to '{destinationRelDirPath}'");

										try
										{
											IReader localReader = reader;
											await Task.Run(
												() =>
												{
													if ( localReader.Cancelled )
														return;
													localReader.WriteEntryToDirectory(
														destinationDirectory,
														ArchiveHelper.DefaultExtractionOptions
													);
												},
												cancellationToken
											);
										}
										catch (ObjectDisposedException)
										{
											return;
										}
										catch ( UnauthorizedAccessException )
										{
											await Logger.LogWarningAsync(
												$"Skipping file '{reader.Entry.Key}' due to lack of permissions."
											);
										}
									}
								}
							}
						}
						finally
						{
							_ = semaphore.Release(); // Release the semaphore slot
						}
					}
				}
			}
			catch ( IOException ex2 )
			{
				await Logger.LogExceptionAsync(ex2);
				return ActionExitCode.IOException;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return ActionExitCode.UnknownError; // Extraction failed
			}
		}


		public void DeleteDuplicateFile(
			DirectoryInfo directoryPath = null,
			string fileExtension = null,
			bool caseInsensitive = true,
			List<string> compatibleExtensions = null
		)
		{
			// internal args
			if ( directoryPath is null )
				directoryPath = RealDestinationPath;
			if ( !(directoryPath is null) && !directoryPath.Exists && MainConfig.CaseInsensitivePathing )
				directoryPath = PathHelper.GetCaseSensitivePath(directoryPath);
			if ( directoryPath?.Exists != true )
				throw new ArgumentException(message: "Invalid directory path.", nameof( directoryPath ));

			var sourceExtensions = Source.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
			compatibleExtensions = compatibleExtensions
				?? (!sourceExtensions.IsNullOrEmptyOrAllNull()
					? sourceExtensions
					: compatibleExtensions)
				?? Game.TextureOverridePriorityList;

			if ( string.IsNullOrEmpty(fileExtension) )
				fileExtension = Arguments;

			FileInfo[] files = directoryPath.GetFilesSafely();
			Dictionary<string, int> fileNameCounts = caseInsensitive
				? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, int>();

			foreach ( FileInfo fileInfo in files )
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
				string thisExtension = fileInfo.Extension;

				bool compatibleExtensionFound = caseInsensitive
					? compatibleExtensions.Any(ext => ext.Equals(thisExtension, StringComparison.OrdinalIgnoreCase))
					: compatibleExtensions.Contains(thisExtension);

				if ( compatibleExtensionFound )
				{
					_ = fileNameCounts.TryGetValue(fileNameWithoutExtension, out int count);
					fileNameCounts[fileNameWithoutExtension] = count + 1;
				}
			}

			foreach ( FileInfo thisFile in files )
			{
				if ( !ShouldDeleteFile(thisFile) )
					continue;

				try
				{
					thisFile.Delete();
					_ = Logger.LogAsync($"Deleted file: '{thisFile}'");
					_ = Logger.LogVerboseAsync(
						$"Leaving alone '{fileNameCounts[Path.GetFileNameWithoutExtension(thisFile.Name)] - 1}' file(s) with the same name of '{Path.GetFileNameWithoutExtension(thisFile.Name)}'."
					);
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex);
				}
			}

			return;

			bool ShouldDeleteFile(FileSystemInfo fileSystemInfoItem)
			{
				string fileName = fileSystemInfoItem.Name;
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
				string fileExtensionFromFile = fileSystemInfoItem.Extension;

				if ( string.IsNullOrEmpty(fileNameWithoutExtension) )
				{
					_ = Logger.LogWarningAsync(
						$"Skipping '{fileName}' Reason: fileNameWithoutExtension is null/empty somehow?"
					);
				}
				else if ( !fileNameCounts.ContainsKey(fileNameWithoutExtension) )
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: Not present in dictionary, ergo does not have a desired extension."
					);
				}
				else if ( fileNameCounts[fileNameWithoutExtension] <= 1 )
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: '{fileNameWithoutExtension}' is the only file with this name."
					);
				}
				else if ( !string.Equals(fileExtensionFromFile, fileExtension, StringComparison.OrdinalIgnoreCase) )
				{
					string caseInsensitivity = caseInsensitive
						? " (case-insensitive)"
						: string.Empty;
					string message =
						$"Skipping '{fileName}' Reason: '{fileExtensionFromFile}' is not the desired extension '{fileExtension}'{caseInsensitivity}";
					_ = Logger.LogVerboseAsync(message);
				}
				else
				{
					return true;
				}

				return false;
			}
		}

		// ReSharper disable once AssignNullToNotNullAttribute
		public ActionExitCode DeleteFile(
			// ReSharper disable once AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null
		)
		{
			if ( sourcePaths is null )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths is null )
				throw new ArgumentNullException(nameof( sourcePaths ));

			try
			{
				// ReSharper disable once PossibleNullReferenceException
				foreach ( string thisFilePath in sourcePaths )
				{
					string realFilePath = thisFilePath;
					if ( MainConfig.CaseInsensitivePathing && !File.Exists(realFilePath) )
						realFilePath = PathHelper.GetCaseSensitivePath(realFilePath).Item1;
					string sourceRelDirPath = MainConfig.SourcePath is null
						? thisFilePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							thisFilePath
						);

					if ( !Path.IsPathRooted(realFilePath) || !File.Exists(realFilePath) )
					{
						var ex = new ArgumentNullException(
							$"Invalid wildcards or file does not exist: '{sourceRelDirPath}'"
						);
						Logger.LogException(ex);
						return ActionExitCode.FileNotFoundPost;
					}

					// Delete the file synchronously
					try
					{
						File.Delete(realFilePath);
						_ = Logger.LogAsync($"Deleting '{sourceRelDirPath}'...");
					}
					catch ( Exception ex )
					{
						Logger.LogException(ex);
						return ActionExitCode.UnknownInnerError;
					}
				}

				if ( sourcePaths.Count == 0 )
				{
					Logger.Log("No files to delete, skipping this instruction.");
				}

				return ActionExitCode.Success;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return ActionExitCode.UnknownError;
			}
		}

		public ActionExitCode RenameFile(
			// ReSharper disable once AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null
		)
		{
			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof( sourcePaths ));

			try
			{
				ActionExitCode exitCode = ActionExitCode.Success;
				// ReSharper disable once PossibleNullReferenceException
				foreach ( string sourcePath in sourcePaths )
				{
					string fileName = Path.GetFileName(sourcePath);
					string sourceRelDirPath = MainConfig.SourcePath is null
						? sourcePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							sourcePath
						);
					// Check if the source file already exists
					if ( !File.Exists(sourcePath) )
					{
						Logger.LogError($"'{sourceRelDirPath}' does not exist!");
						exitCode = ActionExitCode.FileNotFoundPost;
						continue;
					}

					// Check if the destination file already exists
					string destinationFilePath = Path.Combine(
						Path.GetDirectoryName(sourcePath) ?? string.Empty,
						Destination
					);
					string destinationRelDirPath = MainConfig.DestinationPath is null
						? destinationFilePath
						: PathHelper.GetRelativePath(
							MainConfig.DestinationPath.FullName,
							destinationFilePath
						);
					if ( File.Exists(destinationFilePath) )
					{
						if ( !Overwrite )
						{
							exitCode = ActionExitCode.RenameTargetAlreadyExists;
							_ = Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ " skipping file. Reason: Overwrite set to False )"
							);

							continue;
						}

						_ = Logger.LogAsync(
							$"Removing pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
						);
						File.Delete(destinationFilePath);
					}

					// Move the file
					try
					{
						_ = Logger.LogAsync($"Rename '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						File.Move(sourcePath, destinationFilePath);
					}
					catch ( IOException ex )
					{
						// Handle file move error, such as destination file already exists
						exitCode = ActionExitCode.IOException;
						Logger.LogException(ex);
					}
				}

				return exitCode;
			}
			catch ( Exception ex )
			{
				// Handle any unexpected exceptions
				Logger.LogException(ex);
				return ActionExitCode.UnknownError;
			}
		}

		public async Task<ActionExitCode> CopyFileAsync(
			// ReSharper disable twice AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof( sourcePaths ));

			if ( destinationPath?.Exists != true )
				destinationPath = RealDestinationPath;
			if ( destinationPath?.Exists != true )
				throw new ArgumentNullException(nameof( destinationPath ));

			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task CopyIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync(); // Wait for a semaphore slot
					try
					{
						string sourceRelDirPath = MainConfig.SourcePath is null
							? sourcePath
							: PathHelper.GetRelativePath(
								MainConfig.SourcePath.FullName,
								sourcePath
							);
						string fileName = Path.GetFileName(sourcePath);
						string destinationFilePath = MainConfig.CaseInsensitivePathing
							? PathHelper.GetCaseSensitivePath(
								Path.Combine(destinationPath.FullName, fileName),
								isFile: true
							).Item1
							: Path.Combine(destinationPath.FullName, fileName);
						string destinationRelDirPath = MainConfig.DestinationPath is null
							? destinationFilePath
							: PathHelper.GetRelativePath(
								MainConfig.DestinationPath.FullName,
								destinationFilePath
							);

						// Check if the destination file already exists
						if ( File.Exists(destinationFilePath) )
						{
							if ( !Overwrite )
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								);

								return;
							}

							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							);
							File.Delete(destinationFilePath);
						}

						await Logger.LogAsync($"Copy '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						File.Copy(sourcePath, destinationFilePath);
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release(); // Release the semaphore slot
					}
				}

				if ( sourcePaths is null )
					throw new NullReferenceException(nameof( sourcePaths ));

				var tasks = sourcePaths.Select(CopyIndividualFileAsync).ToList();

				try
				{
					await Task.WhenAll(tasks); // Wait for all move tasks to complete
					return ActionExitCode.Success;
				}
				catch
				{
					return ActionExitCode.UnknownError;
				}
			}
		}

		public async Task<ActionExitCode> MoveFileAsync(
			// ReSharper disable twice AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof( sourcePaths ));

			if ( destinationPath?.Exists != true )
				destinationPath = RealDestinationPath;
			if ( destinationPath?.Exists != true )
				throw new ArgumentNullException(nameof( destinationPath ));

			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task MoveIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync(); // Wait for a semaphore slot

					try
					{
						string sourceRelDirPath = MainConfig.SourcePath is null
							? sourcePath
							: PathHelper.GetRelativePath(
								MainConfig.SourcePath.FullName,
								sourcePath
							);
						string fileName = Path.GetFileName(sourcePath);
						string destinationFilePath = MainConfig.CaseInsensitivePathing
							? PathHelper.GetCaseSensitivePath(
								Path.Combine(destinationPath.FullName, fileName),
								isFile: true
							).Item1
							: Path.Combine(destinationPath.FullName, fileName);
						string destinationRelDirPath = MainConfig.DestinationPath is null
							? destinationFilePath
							: PathHelper.GetRelativePath(
								MainConfig.DestinationPath.FullName,
								destinationFilePath
							);

						// Check if the destination file already exists
						if ( File.Exists(destinationFilePath) )
						{
							if ( !Overwrite )
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								);

								return;
							}

							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							);
							File.Delete(destinationFilePath);
						}

						await Logger.LogAsync($"Move '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						File.Move(sourcePath, destinationFilePath);
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release(); // Release the semaphore slot
					}
				}

				if ( sourcePaths is null )
					throw new NullReferenceException(nameof( sourcePaths ));

				var tasks = sourcePaths.Select(MoveIndividualFileAsync).ToList();
				try
				{
					await Task.WhenAll(tasks); // Wait for all move tasks to complete
					return ActionExitCode.Success;
				}
				catch
				{
					return ActionExitCode.UnknownError;
				}
			}
		}


		// todo: define exit codes here.
		public async Task<ActionExitCode> ExecuteTSLPatcherAsync()
		{
			try
			{
				foreach ( string t in RealSourcePaths )
				{
					DirectoryInfo tslPatcherDirectory = File.Exists(t)
						? new FileInfo(t).Directory // It's a file, get the parent folder.
						: new DirectoryInfo(t);     // It's a folder, create a DirectoryInfo instance

					if ( tslPatcherDirectory?.Exists != true )
					{
						throw new DirectoryNotFoundException($"The directory '{t}' could not be located on the disk.");
					}

					//PlaintextLog=0
					string fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.rtf");
					if ( File.Exists(fullInstallLogFile) )
						File.Delete(fullInstallLogFile);
					//PlaintextLog=1
					fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.txt");
					if ( File.Exists(fullInstallLogFile) )
						File.Delete(fullInstallLogFile);

					IniHelper.ReplacePlaintextLog(tslPatcherDirectory);
					IniHelper.ReplaceLookupGameFolder(tslPatcherDirectory);

					string args = $@"""{MainConfig.DestinationPath}""" // arg1 = swkotor directory
						+ $@" ""{tslPatcherDirectory}""" // arg2 = mod directory (where tslpatchdata folder is)
						+ (string.IsNullOrEmpty(Arguments)
							? ""
							: $" {Arguments}"); // arg3 = (optional) install option integer index from namespaces.ini

					string thisExe = null;
					FileInfo tslPatcherCliPath = null;
					switch ( MainConfig.PatcherOption )
					{
						case MainConfig.AvailablePatchers.PyKotorCLI:
							thisExe = Path.Combine(
								path1: "Resources",
								RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
									? "pykotorcli.exe" // windows
									: "pykotorcli"     // linux/mac
							);
							break;
						case MainConfig.AvailablePatchers.TSLPatcher:
						default:
							tslPatcherCliPath = new FileInfo(t);
							break;
					}

					if ( tslPatcherCliPath is null )
					{
						string executingAssemblyLocation = Utility.Utility.GetExecutingAssemblyDirectory();

						tslPatcherCliPath = new FileInfo(Path.Combine(executingAssemblyLocation, thisExe));
					}

					await Logger.LogAsync("Starting TSLPatcher instructions...");
					if ( MainConfig.PatcherOption != MainConfig.AvailablePatchers.TSLPatcher )
						await Logger.LogAsync($"Using CLI to run command: '{tslPatcherCliPath} {args}'");

					// ReSharper disable twice UnusedVariable
					(int exitCode, string output, string error) = await PlatformAgnosticMethods.ExecuteProcessAsync(
						tslPatcherCliPath.FullName,
						args,
						noAdmin: MainConfig.NoAdmin
					);
					await Logger.LogVerboseAsync($"'{tslPatcherCliPath.Name}' exited with exit code {exitCode}");

					try
					{
						List<string> installErrors = VerifyInstall();
						if ( installErrors.Count > 0 )
						{
							await Logger.LogAsync(string.Join(Environment.NewLine, installErrors));
							return ActionExitCode.TSLPatcherError;
						}
					}
					catch ( FileNotFoundException )
					{
						await Logger.LogAsync("No TSLPatcher log file found!");
						return ActionExitCode.TSLPatcherLogNotFound;
					}

					return exitCode == 0
						? ActionExitCode.Success
						: ActionExitCode.TSLPatcherCLIError;
				}

				return ActionExitCode.Success;
			}
			catch ( DirectoryNotFoundException ex )
			{
				await Logger.LogExceptionAsync(ex);
				throw;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				throw;
			}
		}

		public async Task<ActionExitCode> ExecuteProgramAsync([ItemNotNull] List<string> sourcePaths = null)
		{
			try
			{
				if ( sourcePaths == null )
					sourcePaths = RealSourcePaths;
				if ( sourcePaths == null )
					throw new ArgumentNullException(nameof( sourcePaths ));

				ActionExitCode exitCode = ActionExitCode.Success; // Track the success status
				foreach ( string sourcePath in sourcePaths )
				{
					try
					{
						// TODO: add a config option to which installer to use for tslpatcher action.
						DirectoryInfo tslPatcherDirectory = File.Exists(sourcePath)
							? new FileInfo(sourcePath).Directory // It's a file, get the parent folder.
							: new DirectoryInfo(sourcePath);     // It's a folder, create a DirectoryInfo instance
						IniHelper.ReplacePlaintextLog(tslPatcherDirectory);
						IniHelper.ReplaceLookupGameFolder(tslPatcherDirectory);

						(int childExitCode, string output, string error) =
							await PlatformAgnosticMethods.ExecuteProcessAsync(
								sourcePath,
								noAdmin: MainConfig.NoAdmin,
								cmdlineArgs: Utility.Utility.ReplaceCustomVariables(Arguments)
							);

						_ = Logger.LogVerboseAsync(output + Environment.NewLine + error);
						if ( childExitCode == 0 )
							continue;

						exitCode = ActionExitCode.ChildProcessError;
						break;
					}
					catch ( FileNotFoundException ex )
					{
						await Logger.LogExceptionAsync(ex);
						return ActionExitCode.FileNotFoundPost;
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						return ActionExitCode.UnknownInnerError;
					}
				}

				return exitCode;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return ActionExitCode.UnknownError;
			}
		}

		// parse TSLPatcher's installlog.rtf (or installlog.txt) for errors when not using the CLI.
		[NotNull]
		private List<string> VerifyInstall([ItemNotNull] List<string> sourcePaths = null)
		{
			if ( sourcePaths == null )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths == null )
				throw new ArgumentNullException(nameof( sourcePaths ));

			foreach ( string sourcePath in sourcePaths )
			{
				string tslPatcherDirPath = Path.GetDirectoryName(sourcePath)
					?? throw new DirectoryNotFoundException($"Could not retrieve parent directory of '{sourcePath}'.");

				//PlaintextLog=0
				string fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.rtf");
				if ( !File.Exists(fullInstallLogFile) )
				{
					//PlaintextLog=1
					fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.txt");
					if ( !File.Exists(fullInstallLogFile) )
					{
						throw new FileNotFoundException(message: "Install log file not found.", fullInstallLogFile);
					}
				}

				string installLogContent = File.ReadAllText(fullInstallLogFile);

				return installLogContent.Split(Environment.NewLine.ToCharArray()).Where(
					thisLine => thisLine.Contains("Error: ") || thisLine.Contains("[Error]")
				).ToList();
			}

			Logger.LogVerbose("No errors found in TSLPatcher installation log file");
			return new List<string>();
		}

		// this method removes the tslpatcher log file.
		// should be called BEFORE any tslpatcher install takes place from KOTORModSync, never post-install.
		public void CleanupTSLPatcherInstall([ItemNotNull] List<string> sourcePaths = null)
		{
			if ( sourcePaths == null )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths == null )
				throw new ArgumentNullException(nameof( sourcePaths ));

			Logger.LogVerbose("Preparing TSLPatcher install...");
			foreach ( string sourcePath in sourcePaths )
			{
				string tslPatcherDirPath = Path.GetDirectoryName(sourcePath)
					?? throw new DirectoryNotFoundException($"Could not retrieve parent directory of '{sourcePath}'.");

				//PlaintextLog=0
				string fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.rtf");

				if ( !File.Exists(fullInstallLogFile) )
				{
					//PlaintextLog=1
					fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.txt");
					if ( !File.Exists(fullInstallLogFile) )
					{
						Logger.LogVerbose($"No prior install found for {sourcePath}");
						return;
					}
				}

				Logger.LogVerbose($"Delete {fullInstallLogFile}");
				File.Delete(fullInstallLogFile);
			}

			Logger.LogVerbose("Finished cleaning tslpatcher install");
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		[NotNull]
		[ItemNotNull]
		public List<Option> GetChosenOptions() => _parentComponent?.Options.Where(
				x => x != null && x.IsSelected && Source.Contains(x.Guid.ToString(), StringComparer.OrdinalIgnoreCase)
			).ToList()
			?? new List<Option>();
		/*return theseChosenOptions?.Count > 0
		    ? theseChosenOptions
		    : throw new KeyNotFoundException( message: "Could not find chosen option for this instruction" );*/
	}
}
