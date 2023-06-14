// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KOTORModSync.Core;
using NUnit.Framework;
// ReSharper disable ConvertToConstant.Local
#pragma warning disable U2U1000, CS8618, RCS1118 // Mark local variable as const.

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class FileDeletionTests
    {
        private string _testDirectory;

        [OneTimeSetUp]
        public void CreateTestDirectory()
        {
            _testDirectory = Path.Combine( Path.GetTempPath(), "DeleteDuplicateFileTests" );
            _ = Directory.CreateDirectory( _testDirectory );
        }

        [OneTimeTearDown]
        public void DeleteTestDirectory()
        {
            Directory.Delete( _testDirectory, true );
        }

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
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple( () =>
            {
                Assert.That( File.Exists( file1 ) );
                Assert.That( File.Exists( file2 ) );
            } );
        }

        [Test]
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
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( !File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                    Assert.That( File.Exists( file3 ) );
                } );
        }

        [Test]
        public void DeleteDuplicateFile_DuplicateFilesWithSameExtension_AllDuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, "Duplicates" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file.txt" );
            string file2 = Path.Combine( directory, "file (1).txt" );
            string file3 = Path.Combine( directory, "file (2).txt" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
            File.WriteAllText( file3, "Content 3" );
            string fileExtension = ".txt";

            // Act
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple(
                () =>
                {
                    Assert.That( !File.Exists( file1 ) );
                    Assert.That( File.Exists( file2 ) );
                    Assert.That( File.Exists( file3 ) );
                } );
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
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple( () =>
            {
                Assert.That( File.Exists( file1 ) );
                Assert.That( File.Exists( file2 ) );
            } );
        }

        [Test]
        public void DeleteDuplicateFile_EmptyDirectory_NoFilesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, "EmptyDirectory" );
            _ = Directory.CreateDirectory( directory );
            string fileExtension = ".txt";

            // Act
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple( () =>
            {
                Assert.That( Directory.Exists( directory ) );
                Assert.That( Directory.GetFiles( directory ).Length, Is.EqualTo( 0 ) );
            } );
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
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple( () =>
            {
                Assert.That( File.Exists( file1 ) );
                Assert.That( File.Exists( file2 ) );
            } );
        }

        // won't run correctly on windows, this is for github actions only.
        [Test]
        public void DeleteDuplicateFile_CaseSensitiveExtensions_DuplicatesDeleted()
        {
            // Arrange
            string directory = Path.Combine( _testDirectory, "DuplicatesWithCaseInsensitiveExtensions" );
            _ = Directory.CreateDirectory( directory );
            string file1 = Path.Combine( directory, "file.txt" );
            string file2 = Path.Combine( directory, "file.TXT" );
            string file3 = Path.Combine( directory, "file.png" );
            File.WriteAllText( file1, "Content 1" );
            File.WriteAllText( file2, "Content 2" );
            File.WriteAllText( file3, "Content 3" );
            string fileExtension = ".txt";

            // Act
            Instruction.DeleteDuplicateFile( directory, fileExtension );

            // Assert
            Assert.Multiple( () =>
            {
                Assert.That( !File.Exists( file1 ) );
                Assert.That( !File.Exists( file2 ) );
                Assert.That( File.Exists( file3 ) );
            } );
        }
    }
}
