// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using NUnit.Framework.Legacy;

#pragma warning disable CS8604

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class FileChecksumValidatorTests
	{
		[OneTimeSetUp]
		public void CreateTestDirectory()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), path2: "FileChecksumTests");
			_ = Directory.CreateDirectory(_testDirectory);
		}

		[OneTimeTearDown]
		public void DeleteTestDirectory() => Directory.Delete(_testDirectory, recursive: true);

		private string? _testDirectory;

		[Test]
		public async Task ValidateChecksumsAsync_AllMatch_ReturnsTrue()
		{
			// Arrange
			var expectedChecksums = new Dictionary<FileInfo, SHA1>();
			var actualChecksums = new Dictionary<FileInfo, SHA1>();

			// Create test files with the same content
			for ( int i = 1; i <= 5; i++ )
			{
				string filePath = Path.Combine(_testDirectory, $"TestFile{i}.txt");
				await File.WriteAllTextAsync(filePath, $"test content{i}");
				expectedChecksums.Add(new FileInfo(filePath), SHA1.Create());
			}

			foreach ( FileInfo? fileInfo in expectedChecksums.Keys )
			{
				SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async(fileInfo);
				Assert.That(sha1.Hash, Is.Not.Null);
				actualChecksums[fileInfo] = sha1;
			}

			// Act
			var validator = new FileChecksumValidator(_testDirectory, expectedChecksums, actualChecksums);
			bool result = await validator.ValidateChecksumsAsync();

			// Assert
			Assert.That(result);
		}

		[Test]
		public async Task ValidateChecksumsAsync_MismatchedChecksums_ReturnsFalse()
		{
			// Arrange
			string testFolderPath = Path.Combine(Path.GetTempPath(), path2: "KOTORModSyncTests");
			_ = Directory.CreateDirectory(testFolderPath);

			var expectedChecksums = new Dictionary<FileInfo, SHA1>();
			var actualChecksums = new Dictionary<FileInfo, SHA1>();

			// Create test files with different content
			for ( int i = 1; i <= 5; i++ )
			{
				string filePath = Path.Combine(testFolderPath, $"TestFile{i}.txt");
				await File.WriteAllTextAsync(filePath, $"test content {i}");
				expectedChecksums.Add(new FileInfo(filePath), SHA1.Create());
			}

			// Calculate the SHA1 hash for the test files
			foreach ( FileInfo? fileInfo in expectedChecksums.Keys )
			{
				SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async(fileInfo);
				actualChecksums[fileInfo] = sha1;
			}

			// Clean up
			foreach ( FileInfo fileInfo in expectedChecksums.Keys )
			{
				File.Delete(fileInfo.FullName);
			}

			Directory.Delete(testFolderPath, recursive: true);

			// Act
			var validator = new FileChecksumValidator(testFolderPath, expectedChecksums, actualChecksums);
			bool result = await validator.ValidateChecksumsAsync();

			// Assert
			Assert.That(result, Is.False);
		}

		[Test]
		public async Task CalculateSHA1Async_ValidFile_CalculatesChecksum()
		{
			// Arrange
			if ( _testDirectory != null )
			{
				_ = Directory.CreateDirectory(_testDirectory);

				string filePath = Path.Combine(_testDirectory, path2: "TestFile.txt");
				await File.WriteAllTextAsync(filePath, contents: "test content");

				// Calculate expected checksum
				var expectedSha1 = SHA1.Create();
				await using ( FileStream stream = File.OpenRead(filePath) )
				{
					byte[] buffer = new byte[81920];
					int bytesRead;
					do
					{
						bytesRead = await stream.ReadAsync(buffer);
						_ = expectedSha1.TransformBlock(
							buffer,
							inputOffset: 0,
							bytesRead,
							outputBuffer: null,
							outputOffset: 0
						);
					} while ( bytesRead > 0 );
				}

				_ = expectedSha1.TransformFinalBlock(Array.Empty<byte>(), inputOffset: 0, inputCount: 0);
				string expectedChecksum = FileChecksumValidator.Sha1ToString(expectedSha1);

				// Act
				SHA1 actualSha1 = await FileChecksumValidator.CalculateSha1Async(new FileInfo(filePath));
				string actualChecksum = FileChecksumValidator.Sha1ToString(actualSha1);

				// Assert
				Assert.That(actualChecksum, Is.EqualTo(expectedChecksum));
				Console.WriteLine($"Expected: {expectedChecksum} Actual: {actualChecksum}");
			}
		}

		[Test]
		public async Task CalculateSHA1Async_FileDoesNotExist_ReturnsNull()
		{
			// Arrange
			string filePath = Path.Combine(_testDirectory, path2: "NonExistentFile.txt");

			try
			{
				// Act
				SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async(new FileInfo(filePath));

				// Assert
				Assert.That(sha1, Is.Null);
			}
			catch ( FileNotFoundException ) // success
			{ }
			catch ( Exception e )
			{
				Debug.WriteLine(e);
				Console.WriteLine(e);
				throw;
			}
		}

		[Test]
		public async Task CalculateSHA1Async_ValidationConsistency_ChecksumConsistent()
		{
			// Arrange
			_ = Directory.CreateDirectory(_testDirectory);

			string filePath = Path.Combine(_testDirectory, path2: "TestFile.txt");
			await File.WriteAllTextAsync(filePath, contents: "test content");

			string expectedChecksum = string.Empty;

			// Calculate expected checksum
			for ( int i = 0; i < 100; i++ )
			{
				var expectedSha1 = SHA1.Create();
				await using ( FileStream stream = File.OpenRead(filePath) )
				{
					byte[] buffer = new byte[81920];
					int bytesRead;
					do
					{
						bytesRead = await stream.ReadAsync(buffer);
						_ = expectedSha1.TransformBlock(
							buffer,
							inputOffset: 0,
							bytesRead,
							outputBuffer: null,
							outputOffset: 0
						);
					} while ( bytesRead > 0 );
				}

				_ = expectedSha1.TransformFinalBlock(Array.Empty<byte>(), inputOffset: 0, inputCount: 0);
				string currentChecksum = FileChecksumValidator.Sha1ToString(expectedSha1);

				if ( i == 0 )
				{
					expectedChecksum = currentChecksum;
				}
				else
				{
					Assert.That(
						currentChecksum,
						Is.EqualTo(expectedChecksum),
						message: "Checksum consistency check failed."
					);
				}
			}

			// Act
			SHA1 actualSha1 = await FileChecksumValidator.CalculateSha1Async(new FileInfo(filePath));
			string actualChecksum = FileChecksumValidator.Sha1ToString(actualSha1);

			// Assert
			Assert.That(actualChecksum, Is.EqualTo(expectedChecksum));
			Console.WriteLine($"Expected: {expectedChecksum} Actual: {actualChecksum}");
		}

		// Test method
		[Test]
		[Ignore("TestNotFinished")]
		public async Task SaveChecksumsToFileAsync_ValidData_SavesChecksumsToFile()
		{
			// Arrange
			string filePath = Path.Combine(_testDirectory, path2: "Checksums.json");
			var checksums = new Dictionary<string, string>
			{
				{
					_testDirectory, FileChecksumValidator.Sha1ToString(SHA1.Create())
				},
			};

			// Convert the directory paths to DirectoryInfo objects
			var directoryChecksums = checksums.ToDictionary(
				static kvp => new DirectoryInfo(kvp.Key),
				static kvp => SHA1.Create()
			);

			// Act
			await FileChecksumValidator.SaveChecksumsToFileAsync(filePath, directoryChecksums);

			// Assert
			Assert.That(File.Exists(filePath));

			string json = await File.ReadAllTextAsync(filePath);
			Dictionary<DirectoryInfo, SHA1> loadedChecksums =
				JsonConvert.DeserializeObject<Dictionary<DirectoryInfo, SHA1>>(json)!;

			Assert.That(loadedChecksums, Has.Count.EqualTo(directoryChecksums.Count));
			CollectionAssert.AreEquivalent(directoryChecksums.Keys, loadedChecksums!.Keys);
			CollectionAssert.AreEquivalent(directoryChecksums.Values, loadedChecksums.Values);

			// Clean up
			File.Delete(filePath);
		}

		[Test]
		[Ignore("NotFinished")]
		public async Task LoadChecksumsFromFileAsync_FileExists_LoadsChecksums()
		{
			await using ( StringWriter sw = new() )
			{
				Console.SetOut(sw);
				// Arrange
				string testFolderPath = Path.Combine(Path.GetTempPath(), path2: "KOTORModSyncTests");
				_ = Directory.CreateDirectory(testFolderPath);

				string filePath = Path.Combine(testFolderPath, path2: "Checksums.txt");
				var checksums = new Dictionary<string, string>
				{
					{
						Path.Combine(testFolderPath, path2: "TestFile.txt"), "SHA1HashValue"
					},
				};

				// Write checksums to the file
				IEnumerable<string> lines = checksums.Select(static kv => $"{kv.Key},{kv.Value}");
				await File.WriteAllLinesAsync(filePath, lines);

				try
				{
					// Act
					Dictionary<FileInfo, SHA1> loadedChecksums =
						await FileChecksumValidator.LoadChecksumsFromFileAsync(new FileInfo(filePath));

					// Assert
					Assert.That(loadedChecksums, Has.Count.EqualTo(checksums.Count), sw.ToString());

					// Check each loaded checksum
					foreach ( KeyValuePair<FileInfo, SHA1> loadedChecksum in loadedChecksums )
					{
						Assert.Multiple(
							() =>
							{
								Assert.That(
									checksums.ContainsKey(loadedChecksum.Key.FullName),
									$"The loaded checksum for file '{loadedChecksum.Key.FullName}' is missing from the expected checksums."
								);
								Assert.That(
									loadedChecksum.Value.Hash,
									Is.EqualTo(checksums[loadedChecksum.Key.FullName]),
									$"The loaded checksum for file '{loadedChecksum.Key.FullName}' does not match the expected value."
								);
							}
						);
					}
				}
				finally
				{
					// Clean up
					File.Delete(filePath);
					Directory.Delete(testFolderPath);
				}
			}
		}

		[Test]
		public async Task LoadChecksumsFromFileAsync_FileDoesNotExist_ReturnsEmptyDictionary()
		{
			// Arrange
			string filePath = Path.Combine(_testDirectory, path2: "NonExistentChecksums.json");

			// Act
			Dictionary<FileInfo, SHA1> loadedChecksums =
				await FileChecksumValidator.LoadChecksumsFromFileAsync(new FileInfo(filePath));

			// Assert
			Assert.That(loadedChecksums, Is.Not.Null);
			Assert.That(loadedChecksums, Is.Empty);
		}
	}
}
