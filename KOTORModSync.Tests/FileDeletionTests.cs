﻿// Copyright 2021-2023 KOTORModSync
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
            _testDirectory = Path.Combine( Path.GetTempPath(), "DeleteDuplicateFileTests" );
            _ = Directory.CreateDirectory( _testDirectory );
        }

        [OneTimeTearDown]
        public void DeleteTestDirectory() => Directory.Delete( _testDirectory, true );

        private string _testDirectory;

        [Test]
        public void DeleteDuplicateFile_NoDuplicateFiles_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, "NoDuplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file1.txt" );
            string file2 = Path.Combine( directory, "file2.png" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
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
        [Ignore( "todo in DeleteDuplicateFile()" )]
        public void DeleteDuplicateFile_DuplicateFilesWithDifferentExtensions_AllDuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, "Duplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file.txt" );
            string file2 = Path.Combine( directory, "file.png" );
            string file3 = Path.Combine( directory, "file.jpg" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
            File.WriteAllText( file3, "Content 3" );
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
            string directory = Path.Combine( _testDirectory, "Duplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "FILE.Tga" );
            string file2 = Path.Combine( directory, "fIle.tPc" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
            string fileExtension = ".tga";

            // Act
            new Instruction().DeleteDuplicateFile( new DirectoryInfo( directory ), fileExtension );

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
            string directory = Path.Combine( _testDirectory, "InvalidExtension" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file1.txt" );
            string file2 = Path.Combine( directory, "file2.png" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
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
            string directory = Path.Combine( _testDirectory, "EmptyDirectory" );
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
            string directory = Path.Combine( _testDirectory, "DuplicatesWithSubdirectories" );
            _ = Directory.CreateDirectory( directory );
            string subdirectory = Path.Combine( directory, "Subdirectory" );
            _ = Directory.CreateDirectory( subdirectory );
            string file1 = Path.Combine( directory, "file.txt" );
            string file2 = Path.Combine( subdirectory, "file.txt" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
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
            string directory = Path.Combine( _testDirectory, "DuplicatesWithCaseInsensitiveExtensions" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file.tpc" );
            string file2 = Path.Combine( directory, "file.TPC" );
            string file3 = Path.Combine( directory, "file.tga" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
            File.WriteAllText( file3, "Content 3" );
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
