// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.IO.Compression;
using KOTORModSync.Core;

namespace KOTORModSync.Tests
{
	[TestFixture]
	[Ignore("not finished yet")]
	public class FileExtractor
	{
		[SetUp]
		public void Setup()
		{
			// Set up the initial values for destinationPath and sourcePaths
			_destinationPath = new DirectoryInfo("DestinationPath");
			_sourcePaths = new List<string>
			{
				"SourcePath1", "SourcePath2", "SourcePath3",
			};
		}

		[TearDown]
		public void TearDown()
		{
			// Clean up any extracted files or directories after each test if necessary
			if ( _destinationPath is
				{
					Exists: true,
				} )
			{
				_destinationPath.Delete(true);
			}
		}

		private DirectoryInfo? _destinationPath;
		private List<string>? _sourcePaths;

		[Test]
		public async Task ExtractFileAsync_ValidArchive_Success()
		{
			// Arrange
			string archivePath = CreateTemporaryArchive("validArchive.zip");
			_sourcePaths = new List<string>
			{
				archivePath,
			};

			// Act
			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);
					// Add more specific assertions if necessary
					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		[Test]
		public async Task ExtractFileAsync_InvalidArchive_Failure()
		{
			// Arrange
			string archivePath = CreateTemporaryArchive("invalidArchive.zip");
			_sourcePaths = new List<string>
			{
				archivePath,
			};

			// Act
			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);
					// Add more specific assertions if necessary
					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.False);
				}
			);
		}

		[Test]
		[Ignore("not finished yet")]
		public async Task ExtractFileAsync_SelfExtractingExe_Success()
		{
			// Arrange
			//string archivePath = CreateTemporarySelfExtractingExe( "selfExtracting.exe" );
			//_sourcePaths = new List<string> { archivePath };

			// Act
			if ( _sourcePaths is null )
				throw new NullReferenceException(nameof( _sourcePaths ));

			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);
					// Add more specific assertions if necessary
					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		[Test]
		public async Task ExtractFileAsync_PermissionDenied_SkipsFile()
		{
			// Arrange
			string archivePath = CreateTemporaryArchive("archiveWithPermissionDenied.zip");
			_sourcePaths = new List<string>
			{
				archivePath,
			};

			// Act
			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);
					// Add more specific assertions if necessary
					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		// Helper methods to create temporary archive files
		private static string CreateTemporaryArchive(string fileName)
		{
			string archivePath = Path.Combine(Path.GetTempPath(), fileName);
			ZipFile.CreateFromDirectory(sourceDirectoryName: "SourceDirectory", archivePath);
			return archivePath;
		}

		/*private string CreateTemporarySelfExtractingExe( string fileName )
		{
		    string exePath = Path.Combine( Path.GetTempPath(), fileName );

		    using ( ZipFile zip = new ZipFile() )
		    {
		        // Add files to the archive
		        zip.AddFile( "File1.txt" );
		        zip.AddFile( "File2.txt" );

		        // Set the self-extracting options
		        SelfExtractorSaveOptions options = new SelfExtractorSaveOptions
		        {
		            Flavor = SelfExtractorFlavor.ConsoleApplication,
		            DefaultExtractDirectory = _destinationPath?.FullName,
		            ExtractExistingFile = ExtractExistingFileAction.OverwriteSilently,
		            RemoveUnpackedFilesAfterExecute = true,
		            Quiet = true
		        };

		        // Save the archive as a self-extracting executable
		        zip.SaveSelfExtractor( exePath, options );
		    }

		    return exePath;
		}*/
	}
}
