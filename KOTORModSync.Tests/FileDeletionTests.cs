// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

// ReSharper disable RedundantUsingDirective

using KOTORModSync.Core;

// ReSharper disable ConvertToConstant.Local
#pragma warning disable U2U1000, CS8618, RCS1118 // Mark local variable as const.

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class FileDeletionTests
    {
        [OneTimeSetUp]
        public void CreateTestDirectory()
        {
            _testDirectory = Path.Combine( Path.GetTempPath(), path2: "DeleteDuplicateFileTests" );
            _ = Directory.CreateDirectory( _testDirectory );
        }

        [OneTimeTearDown]
        public void DeleteTestDirectory() => Directory.Delete( _testDirectory, recursive: true );

        private string _testDirectory;

        [Test]
        public void DeleteDuplicateFile_NoDuplicateFiles_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "NoDuplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, path2: "file1.txt" );
            string file2 = Path.Combine( directory, path2: "file2.png" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            string fileExtension = ".txt";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension, true );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                }
            );
        }

        [Test]
        [Ignore( "todo in DeleteDuplicateFile()" )]
        public void DeleteDuplicateFile_DuplicateFilesWithDifferentExtensions_AllDuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "Duplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, path2: "file.txt" );
            string file2 = Path.Combine( directory, path2: "file.png" );
            string file3 = Path.Combine( directory, path2: "file.jpg" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            File.WriteAllText( file3, contents: "Content 3" );
            string fileExtension = ".txt";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( !File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                    Assert.That( File.Exists( file3 ) );
                }
            );
        }

        [Test]
        public void DeleteDuplicateFile_CaseInsensitiveFileNames_DuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "Duplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, path2: "FILE.tga" );
            string file2 = Path.Combine( directory, path2: "fIle.tpc" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            string fileExtension = ".tga";

            // Act
            new Instruction().DeleteDuplicateFile(
                new DirectoryInfo( directory ),
                fileExtension,
                caseInsensitive: true
            );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( !File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                }
            );
        }

        [Test]
        public void DeleteDuplicateFile_InvalidFileExtension_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "InvalidExtension" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, path2: "file1.txt" );
            string file2 = Path.Combine( directory, path2: "file2.png" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            string fileExtension = ".txt";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                }
            );
        }

        [Test]
        public void DeleteDuplicateFile_EmptyDirectory_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "EmptyDirectory" );
            _ = Directory.CreateDirectory( directory );
            string fileExtension = ".txt";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( Directory.Exists( directory ) );
                    Assert.That( Directory.GetFiles( directory ), Is.Empty );
                }
            );
        }

        [Test]
        public void DeleteDuplicateFile_DuplicateFilesInSubdirectories_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "DuplicatesWithSubdirectories" );
            _ = Directory.CreateDirectory( directory );
            string subdirectory = Path.Combine( directory, path2: "Subdirectory" );
            _ = Directory.CreateDirectory( subdirectory );
            string file1 = Path.Combine( directory, path2: "file.txt" );
            string file2 = Path.Combine( subdirectory, path2: "file.txt" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            string fileExtension = ".txt";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                }
            );
        }

        // won't run correctly on windows, this is for github actions only.
        [Test]
        public void DeleteDuplicateFile_CaseSensitiveExtensions_DuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, path2: "DuplicatesWithCaseInsensitiveExtensions" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, path2: "file.tpc" );
            string file2 = Path.Combine( directory, path2: "file.TPC" );
            string file3 = Path.Combine( directory, path2: "file.tga" );
            File.WriteAllText( file1, contents: "Content 1" );
            File.WriteAllText( file2, contents: "Content 2" );
            File.WriteAllText( file3, contents: "Content 3" );
            string fileExtension = ".tpc";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( !File.Exists( file1 ) );
                    Assert.That( !File.Exists( file2 ) );
                    Assert.That( File.Exists( file3 ) );
                }
            );
        }
    }
}
