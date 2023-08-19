// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    internal class PathCaseSensitivityTests
    {
#pragma warning disable CS8618
        private static string s_testDirectory;
#pragma warning restore CS8618

        [SetUp]
        public static void InitializeTestDirectory()
        {
            s_testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory( s_testDirectory );
        }

        [TearDown]
        public static void CleanUpTestDirectory() => Directory.Delete(s_testDirectory, recursive: true);

        [Test]
        public void TestDuplicatesWithFileInfo()
        {
            File.WriteAllText(Path.Combine(s_testDirectory, "file.txt"), "Test content");
            File.WriteAllText(Path.Combine(s_testDirectory, "File.txt"), "Test content");
            
            var fileInfo = new FileInfo(Path.Combine(s_testDirectory, "file.txt"));
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(fileInfo).ToList();
            var failureMessage = new StringBuilder();
            foreach (FileSystemInfo item in result)
            {
                failureMessage.AppendLine(item.FullName);
            }
            
            Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result?.Count}. Output: {failureMessage}");
        }

        [Test]
        public void TestDuplicatesWithDirectoryNameString()
        {
            File.WriteAllText(Path.Combine(s_testDirectory, "file.txt"), "Test content");
            File.WriteAllText(Path.Combine(s_testDirectory, "File.txt"), "Test content");

            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(s_testDirectory).ToList();
            var failureMessage = new StringBuilder();
            foreach (FileSystemInfo item in result)
            {
                failureMessage.AppendLine(item.FullName);
            }
            
            Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result?.Count}. Output: {failureMessage}");
        }

        [Test]
        public void TestDuplicateDirectories()
        {
            _ = Directory.CreateDirectory( Path.Combine( s_testDirectory, "subdir" ) );
            _ = Directory.CreateDirectory( Path.Combine( s_testDirectory, "SubDir" ) );
            
            var dirInfo = new DirectoryInfo(s_testDirectory);
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(dirInfo).ToList();
            var failureMessage = new StringBuilder();
            foreach (FileSystemInfo item in result)
            {
                failureMessage.AppendLine(item.FullName);
            }
            
            Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result?.Count}. Output: {failureMessage}");
        }

        [Test]
        public void TestDuplicatesWithDifferentCasingFilesInNestedDirectories()
        {
            string subDirectory = Path.Combine(s_testDirectory, "SubDirectory");
            _ = Directory.CreateDirectory( subDirectory );
            
            File.WriteAllText(Path.Combine(s_testDirectory, "file.txt"), "Test content");
            File.WriteAllText(Path.Combine(subDirectory, "FILE.txt"), "Test content");
            
            var dirInfo = new DirectoryInfo(s_testDirectory);
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(dirInfo, includeSubFolders: true).ToList();
            var failureMessage = new StringBuilder();
            foreach (FileSystemInfo item in result)
            {
                failureMessage.AppendLine(item.FullName);
            }

            Assert.That( result, Has.Count.EqualTo(4), $"Expected 4 items, but found {result?.Count}. Output: {failureMessage}");
        }

        [Test]
        public void TestDuplicateNestedDirectories()
        {
            string subDir1 = Path.Combine(s_testDirectory, "SubDir");
            string subDir2 = Path.Combine(s_testDirectory, "subdir");

            _ = Directory.CreateDirectory( subDir1 );
            _ = Directory.CreateDirectory( subDir2 );
            
            File.WriteAllText(Path.Combine(subDir1, "file.txt"), "Test content");
            File.WriteAllText(Path.Combine(subDir2, "file.txt"), "Test content");
            
            var dirInfo = new DirectoryInfo(s_testDirectory);
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(dirInfo, includeSubFolders: true).ToList();
            var failureMessage = new StringBuilder();
            foreach (FileSystemInfo item in result)
            {
                failureMessage.AppendLine(item.FullName);
            }
            
            Assert.That( result, Has.Count.EqualTo( 4 ), $"Expected 4 items, but found {result?.Count}. Output: {failureMessage}");
        }
        
        [Test]
        public void TestInvalidPath()
        {
            ArgumentException? ex = Assert.Throws<ArgumentException>(
                () => PathHelper.FindCaseInsensitiveDuplicates( "Invalid>Path" )?.ToList()
            );
        }

        [Test]
        public void GetCaseSensitivePath_ValidFile_ReturnsSamePath()
        {
            // Arrange
            string testFilePath = Path.Combine(s_testDirectory, "test.txt");
            File.Create(testFilePath).Close();

            // Act
            string? result = PathHelper.GetCaseSensitivePath(testFilePath);

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
            string? result = PathHelper.GetCaseSensitivePath(testDirPath);

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
            string upperCasePath = invalidPath.ToUpperInvariant();

            // Act & Assert
            _ = Assert.Throws<ArgumentException>( () => PathHelper.GetCaseSensitivePath( upperCasePath ) );
        }

        [Test]
        public void GetCaseSensitivePath_RelativePath_ReturnsAbsolutePath()
        {
            // Arrange
            string testFilePath = Path.Combine(s_testDirectory, "test.txt");
            File.Create(testFilePath).Close();
            string relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), testFilePath);
            string upperCasePath = relativePath.ToUpperInvariant();

            // Act
            string? result = PathHelper.GetCaseSensitivePath(upperCasePath);

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
            string upperCasePath = testFilePath.ToUpperInvariant();

            // Act
            string? result = PathHelper.GetCaseSensitivePath( upperCasePath );

            // Assert
            Assert.That( result, Is.EqualTo( testFilePath ) );
        }

        [Test]
        public void GetCaseSensitivePath_NonExistentFile_ReturnsCaseSensitivePath()
        {
            // Arrange
            string nonExistentFileName = "non_existent_file.txt";
            string nonExistentFilePath = Path.Combine(s_testDirectory, nonExistentFileName);
            string upperCasePath = nonExistentFilePath.ToUpperInvariant();

            // Act
            string? result = PathHelper.GetCaseSensitivePath(upperCasePath);

            // Assert
            Assert.That( result, Is.EqualTo( Path.Combine(s_testDirectory, nonExistentFileName.ToUpperInvariant()) ) );
        }

        [Test]
        public void GetCaseSensitivePath_NonExistentDirAndChildFile_ReturnsCaseSensitivePath()
        {
            // Arrange
            string nonExistentRelFilePath = Path.Combine( "non_existent_dir", "non_existent_file.txt" );
            string nonExistentFilePath = Path.Combine(s_testDirectory, nonExistentRelFilePath);
            string upperCasePath = nonExistentFilePath.ToUpperInvariant();

            // Act
            string? result = PathHelper.GetCaseSensitivePath(upperCasePath);

            // Assert
            Assert.That(result, Is.EqualTo(Path.Combine( s_testDirectory, nonExistentRelFilePath.ToUpperInvariant() )));
        }

        [Test]
        public void GetCaseSensitivePath_NonExistentDirectory_ReturnsCaseSensitivePath()
        {
            // Arrange
            string nonExistentRelPath = Path.Combine( "non_existent_dir", "non_existent_child_dir" );
            string nonExistentDirPath = Path.Combine(s_testDirectory, nonExistentRelPath);
            string upperCasePath = nonExistentDirPath.ToUpperInvariant();

            // Act
            string? result = PathHelper.GetCaseSensitivePath(upperCasePath);

            // Assert
            Assert.That( result, Is.EqualTo( Path.Combine(s_testDirectory, nonExistentRelPath.ToUpperInvariant()) ) );
        }
    }
}
