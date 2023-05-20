// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Security.Cryptography;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.IO;

namespace KOTORModSync.Tests
{
    [TestFixture]
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
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(TestFolderPath, $"TestFile{i}.txt");
                File.WriteAllText(filePath, "test content");
                expectedChecksums.Add(new FileInfo(filePath), SHA1.Create());
            }

            // Calculate the SHA1 hash for the test files
            foreach (KeyValuePair<FileInfo, SHA1> expectedChecksum in expectedChecksums)
            {
                FileInfo fileInfo = expectedChecksum.Key;
                SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(fileInfo);
                actualChecksums[fileInfo.Name] = BitConverter.ToString(sha1.Hash).Replace("-", "");
            }

            // Act
            var validator = new FileChecksumValidator(TestFolderPath, expectedChecksums, expectedChecksums);
            bool result = await validator.ValidateChecksumsAsync();

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task ValidateChecksumsAsync_MismatchedChecksums_ReturnsFalse()
        {
            // Arrange
            string testFolderPath = Path.Combine(Path.GetTempPath(), "KOTORModSyncTests");
            Directory.CreateDirectory(testFolderPath);

            var expectedChecksums = new Dictionary<FileInfo, SHA1>();
            var actualChecksums = new Dictionary<FileInfo, SHA1>();

            // Create test files with different content
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(testFolderPath, $"TestFile{i}.txt");
                File.WriteAllText(filePath, $"test content {i}");
                expectedChecksums.Add(new FileInfo(filePath), SHA1.Create());
            }

            // Calculate the SHA1 hash for the test files
            foreach (KeyValuePair<FileInfo, SHA1> expectedChecksum in expectedChecksums)
            {
                FileInfo fileInfo = expectedChecksum.Key;
                SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(fileInfo);
                actualChecksums[fileInfo] = sha1;
            }

            // Clean up
            foreach (FileInfo fileInfo in expectedChecksums.Keys)
            {
                File.Delete(fileInfo.FullName);
            }
            Directory.Delete(testFolderPath, true);


            // Act
            var validator = new FileChecksumValidator(testFolderPath, expectedChecksums, actualChecksums);
            bool result = await validator.ValidateChecksumsAsync();

            // Assert
            Assert.IsFalse(result);
        }



        [Test]
        public async Task CalculateSHA1Async_ValidFile_CalculatesChecksum()
        {
            // Arrange
            string filePath = Path.Combine(TestFolderPath, "TestFile.txt");
            File.WriteAllText(filePath, "test content");

            // Act
            SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(new FileInfo(filePath));
            string actualChecksum = FileChecksumValidator.SHA1ToString(sha1);

            // Assert
            string expectedChecksum = FileChecksumValidator.StringToSHA1("test content");
            Assert.AreEqual(expectedChecksum, actualChecksum);
        }

        [Test]
        public async Task CalculateSHA1Async_FileDoesNotExist_ReturnsNull()
        {
            // Arrange
            string filePath = Path.Combine(TestFolderPath, "NonExistentFile.txt");

            // Act
            SHA1 sha1 = await FileChecksumValidator.CalculateSHA1Async(new FileInfo(filePath));

            // Assert
            Assert.IsNull(sha1);
        }

        [Test]
        public async Task SaveChecksumsToFileAsync_ValidData_SavesChecksumsToFile()
        {
            // Arrange
            string filePath = Path.Combine(TestFolderPath, "Checksums.json");
            var checksums = new Dictionary<DirectoryInfo, SHA1>
            {
                { new DirectoryInfo(TestFolderPath), SHA1.Create() }
            };

            // Act
            await FileChecksumValidator.SaveChecksumsToFileAsync(filePath, checksums);

            // Assert
            Assert.IsTrue(File.Exists(filePath));

            string json = await File.ReadAllTextAsync(filePath);
            var loadedChecksums = JsonConvert.DeserializeObject<Dictionary<DirectoryInfo, SHA1>>(json);

            Assert.AreEqual(checksums.Count, loadedChecksums.Count);
            CollectionAssert.AreEquivalent(checksums.Keys, loadedChecksums.Keys);
            CollectionAssert.AreEquivalent(checksums.Values.Select(FileChecksumValidator.SHA1ToString), loadedChecksums.Values.Select(FileChecksumValidator.SHA1ToString));

            // Clean up
            File.Delete(filePath);
        }

        [Test]
        public async Task LoadChecksumsFromFileAsync_FileExists_LoadsChecksums()
        {
            using (StringWriter sw = new StringWriter())
            {
                Console.SetOut(sw);
                // Arrange
                string testFolderPath = Path.Combine(Path.GetTempPath(), "KOTORModSyncTests");
                Directory.CreateDirectory(testFolderPath);

                string filePath = Path.Combine(testFolderPath, "Checksums.txt");
                var checksums = new Dictionary<string, string>
                {
                    { Path.Combine(testFolderPath, "TestFile.txt"), "SHA1HashValue" }
                };

                // Write checksums to the file
                var lines = checksums.Select(kv => $"{kv.Key},{kv.Value}");
                await File.WriteAllLinesAsync(filePath, lines);

                try
                {
                    // Act
                    Dictionary<FileInfo, SHA1> loadedChecksums = await FileChecksumValidator.LoadChecksumsFromFileAsync(new FileInfo(filePath));

                    // Assert
                    Assert.That(loadedChecksums.Count, Is.EqualTo(checksums.Count), sw.ToString());

                    // Check each loaded checksum
                    foreach (var loadedChecksum in loadedChecksums)
                    {
                        Assert.IsTrue(checksums.ContainsKey(loadedChecksum.Key.FullName), $"The loaded checksum for file '{loadedChecksum.Key.FullName}' is missing from the expected checksums.");
                        Assert.AreEqual(checksums[loadedChecksum.Key.FullName], loadedChecksum.Value.Hash, $"The loaded checksum for file '{loadedChecksum.Key.FullName}' does not match the expected value.");
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
            string filePath = Path.Combine(TestFolderPath, "NonExistentChecksums.json");

            // Act
            Dictionary<FileInfo, SHA1> loadedChecksums = await FileChecksumValidator.LoadChecksumsFromFileAsync(new FileInfo(filePath));

            // Assert
            Assert.IsNotNull(loadedChecksums);
            Assert.IsEmpty(loadedChecksums);
        }
    }
    public class FileInfoConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FileInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string filePath = (string)reader.Value;
            return new FileInfo(filePath);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            FileInfo fileInfo = (FileInfo)value;
            writer.WriteValue(fileInfo.FullName);
        }
    }
}
