// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
    [TestFixture]
    [Ignore( "NotSupportedYet" )]
    public class FileChecksumValidatorTests
    {
        private const string TestFolderPath = "TestFiles";

        [Test]
        public async Task ValidateChecksumsAsync_AllMatch_ReturnsTrue()
        {
            // Arrange
            var expectedChecksums = new Dictionary<FileInfo, SHA1>();
            var actualChecksums = new Dictionary<string, string>();

            // Create test files with the same content
            for ( int i = 1; i <= 5; i++ )
            {
                string filePath = Path.Combine( TestFolderPath, $"TestFile{i}.txt" );
                await File.WriteAllTextAsync( filePath, "test content" );
                expectedChecksums.Add( new FileInfo( filePath ), SHA1.Create() );
            }

            foreach ( FileInfo fileInfo in expectedChecksums.Select( static expectedChecksum => expectedChecksum.Key ) )
            {
                SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async( fileInfo );
                Assert.That( sha1.Hash, Is.Not.EqualTo( null ) );
                actualChecksums[fileInfo.Name]
                    = BitConverter.ToString( sha1.Hash ?? Array.Empty<byte>() ).Replace( "-", "" );
            }

            // Act
            var validator = new FileChecksumValidator( TestFolderPath, expectedChecksums, expectedChecksums );
            bool result = await validator.ValidateChecksumsAsync();

            // Assert
            Assert.That( result );
        }

        [Test]
        public async Task ValidateChecksumsAsync_MismatchedChecksums_ReturnsFalse()
        {
            // Arrange
            string testFolderPath = Path.Combine( Path.GetTempPath(), "KOTORModSyncTests" );
            _ = Directory.CreateDirectory( testFolderPath );

            var expectedChecksums = new Dictionary<FileInfo, SHA1>();
            var actualChecksums = new Dictionary<FileInfo, SHA1>();

            // Create test files with different content
            for ( int i = 1; i <= 5; i++ )
            {
                string filePath = Path.Combine( testFolderPath, $"TestFile{i}.txt" );
                await File.WriteAllTextAsync( filePath, $"test content {i}" );
                expectedChecksums.Add( new FileInfo( filePath ), SHA1.Create() );
            }

            // Calculate the SHA1 hash for the test files
            foreach ( FileInfo? fileInfo in expectedChecksums.Select( expectedChecksum => expectedChecksum.Key ) )
            {
                SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async( fileInfo );
                actualChecksums[fileInfo] = sha1;
            }

            // Clean up
            foreach ( FileInfo fileInfo in expectedChecksums.Keys )
                File.Delete( fileInfo.FullName );

            Directory.Delete( testFolderPath, true );

            // Act
            var validator = new FileChecksumValidator( testFolderPath, expectedChecksums, actualChecksums );
            bool result = await validator.ValidateChecksumsAsync();

            // Assert
            Assert.That( result, Is.False );
        }

        [Test]
        public async Task CalculateSHA1Async_ValidFile_CalculatesChecksum()
        {
            // Arrange
            string filePath = Path.Combine( TestFolderPath, "TestFile.txt" );
            await File.WriteAllTextAsync( filePath, "test content" );

            // Act
            SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async( new FileInfo( filePath ) );
            string actualChecksum = FileChecksumValidator.Sha1ToString( sha1 );

            // Assert
            string expectedChecksum = FileChecksumValidator.StringToSha1( "test content" );
            Assert.That( actualChecksum, Is.EqualTo( expectedChecksum ) );
        }

        [Test]
        public async Task CalculateSHA1Async_FileDoesNotExist_ReturnsNull()
        {
            // Arrange
            string filePath = Path.Combine( TestFolderPath, "NonExistentFile.txt" );

            // Act
            SHA1 sha1 = await FileChecksumValidator.CalculateSha1Async( new FileInfo( filePath ) );

            // Assert
            Assert.That( sha1, Is.Null );
        }

        [Test]
        public async Task SaveChecksumsToFileAsync_ValidData_SavesChecksumsToFile()
        {
            // Arrange
            string filePath = Path.Combine( TestFolderPath, "Checksums.json" );
            var checksums = new Dictionary<DirectoryInfo, SHA1>
            {
                { new DirectoryInfo( TestFolderPath ), SHA1.Create() }
            };

            // Act
            await FileChecksumValidator.SaveChecksumsToFileAsync( filePath, checksums );

            // Assert
            Assert.That( File.Exists( filePath ) );

            string json = await File.ReadAllTextAsync( filePath );
            Dictionary<DirectoryInfo, SHA1>? loadedChecksums
                = JsonConvert.DeserializeObject<Dictionary<DirectoryInfo, SHA1>>( json );

            Assert.That( loadedChecksums?.Count, Is.EqualTo( checksums.Count ) );
            CollectionAssert.AreEquivalent( checksums.Keys, loadedChecksums.Keys );
            CollectionAssert.AreEquivalent(
                checksums.Values.Select( FileChecksumValidator.Sha1ToString ),
                loadedChecksums.Values.Select( FileChecksumValidator.Sha1ToString )
            );

            // Clean up
            File.Delete( filePath );
        }

        [Test]
        public async Task LoadChecksumsFromFileAsync_FileExists_LoadsChecksums()
        {
            await using ( StringWriter sw = new() )
            {
                Console.SetOut( sw );
                // Arrange
                string testFolderPath = Path.Combine( Path.GetTempPath(), "KOTORModSyncTests" );
                _ = Directory.CreateDirectory( testFolderPath );

                string filePath = Path.Combine( testFolderPath, "Checksums.txt" );
                var checksums = new Dictionary<string, string>
                {
                    { Path.Combine( testFolderPath, "TestFile.txt" ), "SHA1HashValue" }
                };

                // Write checksums to the file
                IEnumerable<string> lines = checksums.Select( static kv => $"{kv.Key},{kv.Value}" );
                await File.WriteAllLinesAsync( filePath, lines );

                try
                {
                    // Act
                    Dictionary<FileInfo, SHA1> loadedChecksums
                        = await FileChecksumValidator.LoadChecksumsFromFileAsync( new FileInfo( filePath ) );

                    // Assert
                    Assert.That( loadedChecksums, Has.Count.EqualTo( checksums.Count ), sw.ToString() );

                    // Check each loaded checksum
                    foreach ( KeyValuePair<FileInfo, SHA1> loadedChecksum in loadedChecksums )
                        Assert.Multiple(
                            () =>
                            {
                                Assert.That(
                                    checksums.ContainsKey( loadedChecksum.Key.FullName ),
                                    $"The loaded checksum for file '{loadedChecksum.Key.FullName}' is missing from the expected checksums."
                                );
                                Assert.That(
                                    loadedChecksum.Value.Hash,
                                    Is.EqualTo( checksums[loadedChecksum.Key.FullName] ),
                                    $"The loaded checksum for file '{loadedChecksum.Key.FullName}' does not match the expected value."
                                );
                            }
                        );
                }
                finally
                {
                    // Clean up
                    File.Delete( filePath );
                    Directory.Delete( testFolderPath );
                }
            }
        }

        [Test]
        public async Task LoadChecksumsFromFileAsync_FileDoesNotExist_ReturnsEmptyDictionary()
        {
            // Arrange
            string filePath = Path.Combine( TestFolderPath, "NonExistentChecksums.json" );

            // Act
            Dictionary<FileInfo, SHA1> loadedChecksums
                = await FileChecksumValidator.LoadChecksumsFromFileAsync( new FileInfo( filePath ) );

            // Assert
            Assert.That( loadedChecksums, Is.Not.Null );
            Assert.That( loadedChecksums, Is.Empty );
        }
    }

    public class FileInfoConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType ) => objectType == typeof( FileInfo );

        public override object? ReadJson
            ( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer ) =>
            reader.Value is not string filePath ? default : (object)new FileInfo( filePath );

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if ( value is not FileInfo fileInfo )
            {
                return;
            }

            writer.WriteValue( fileInfo.FullName );
        }
    }
}
