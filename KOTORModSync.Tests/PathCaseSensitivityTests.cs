// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    internal class PathCaseSensitivityTests
    {
#pragma warning disable CS8618
        private static string s_testDirectory;
#pragma warning restore CS8618

        [OneTimeSetUp]
        public static void InitializeTestDirectory()
        {
            s_testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory( s_testDirectory );
        }

        [OneTimeTearDown]
        public static void CleanUpTestDirectory() => Directory.Delete(s_testDirectory, true);

        [Test]
        public void GetCaseSensitivePath_ValidFile_ReturnsSamePath()
        {
            // Arrange
            string testFilePath = Path.Combine(s_testDirectory, "test.txt");
            File.Create(testFilePath).Close();

            // Act
            string result = PathHelper.GetCaseSensitivePath(testFilePath);

            // Assert
            Assert.That( result, Is.EqualTo( testFilePath ) );
        }

        [Test]
        public void GetCaseSensitivePath_ValidDirectory_ReturnsSamePath()
        {
            // Arrange
            string testDirPath = Path.Combine(s_testDirectory, "testDir");
            _ = Directory.CreateDirectory( testDirPath );

            // Act
            string result = PathHelper.GetCaseSensitivePath(testDirPath);

            // Assert
            Assert.That( result, Is.EqualTo( testDirPath ) );
        }

        [Test]
        public void GetCaseSensitivePath_NullOrWhiteSpacePath_ThrowsArgumentException()
        {
            // Arrange
            string? nullPath = null;
            string emptyPath = string.Empty;
            const string whiteSpacePath = "   ";

            // Act & Assert
            _ = Assert.Throws<ArgumentException>( () => PathHelper.GetCaseSensitivePath( nullPath ) );
            _ = Assert.Throws<ArgumentException>( () => PathHelper.GetCaseSensitivePath( emptyPath ) );
            _ = Assert.Throws<ArgumentException>( () => PathHelper.GetCaseSensitivePath( whiteSpacePath ) );
        }

        [Test]
        public void GetCaseSensitivePath_InvalidCharactersInPath_ThrowsArgumentException()
        {
            // Arrange
            string invalidPath = Path.Combine(s_testDirectory, "invalid>path");

            // Act & Assert
            _ = Assert.Throws<ArgumentException>( () => PathHelper.GetCaseSensitivePath( invalidPath ) );
        }

        [Test]
        public void GetCaseSensitivePath_RelativePath_ReturnsAbsolutePath()
        {
            // Arrange
            string testFilePath = Path.Combine(s_testDirectory, "test.txt");
            File.Create(testFilePath).Close();
            string relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), testFilePath);

            // Act
            string result = PathHelper.GetCaseSensitivePath(relativePath);

            // Assert
            Assert.That( result, Is.EqualTo( testFilePath ) );
        }

        

        [Test]
        // TODO: doesn't work correctly on windows (returns "...Data\\Local\\Temp\\426FCFF0-3DC3-4FD7-9C7A-D6C0878DACDF\\test.txt" instead of "...Data\\Local\\Temp\\426fcff0-3dc3-4fd7-9c7a-d6c0878dacdf\\test.txt")
        public void GetCaseSensitivePath_EntirePathCaseIncorrect_ReturnsCorrectPath()
        {
            // Arrange
            string testFilePath = Path.Combine(s_testDirectory, "test.txt");
            File.Create(testFilePath).Close();
            string relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), testFilePath);

            // Act
            string result = PathHelper.GetCaseSensitivePath( relativePath.ToUpperInvariant() );

            // Assert
            Assert.That( result, Is.EqualTo( testFilePath ) );
        }

        [Test]
        public void GetCaseSensitivePath_NonExistentFile_ReturnsNull()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(s_testDirectory, "non_existent_file.txt");

            // Act
            string result = PathHelper.GetCaseSensitivePath(nonExistentFilePath);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetCaseSensitivePath_NonExistentDirectory_ReturnsNull()
        {
            // Arrange
            string nonExistentDirPath = Path.Combine(s_testDirectory, "non_existent_dir");

            // Act
            string result = PathHelper.GetCaseSensitivePath(nonExistentDirPath);

            // Assert
            Assert.IsNull(result);
        }
    }
}
