// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Moq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using static KOTORModSync.Core.Utility.Utility;

namespace KOTORModSync.Tests
{
    [TestFixture]
    [Ignore( "not finished yet" )]
    public class FileExtractor
    {
        private DirectoryInfo? _destinationPath;
        private List<string>? _sourcePaths;

        [SetUp]
        public void Setup()
        {
            // Set up the initial values for destinationPath and sourcePaths
            _destinationPath = new DirectoryInfo( "DestinationPath" );
            _sourcePaths = new List<string>
            {
                "SourcePath1",
                "SourcePath2",
                "SourcePath3"
            };
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any extracted files or directories after each test if necessary
            if ( _destinationPath.Exists )
            {
                _destinationPath.Delete( true );
            }
        }

        [Test]
        public async Task ExtractFileAsync_ValidArchive_Success()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            string archivePath = CreateTemporaryArchive( "validArchive.zip" );
            _sourcePaths = new List<string> { archivePath };

            // Act
            bool extractionResult = await new Instruction().ExtractFileAsync( _destinationPath, _sourcePaths );

            // Assert
            Assert.IsTrue( extractionResult );
            // Add more specific assertions if necessary
            Assert.IsTrue( Directory.Exists( _destinationPath.FullName ) );
            // Add additional assertions to validate the extracted files/directories
        }

        [Test]
        public async Task ExtractFileAsync_InvalidArchive_Failure()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            string archivePath = CreateTemporaryArchive( "invalidArchive.zip" );
            _sourcePaths = new List<string> { archivePath };

            // Act
            bool extractionResult = await new Instruction().ExtractFileAsync( _destinationPath, _sourcePaths );

            // Assert
            Assert.IsFalse( extractionResult );
            // Add more specific assertions if necessary
            Assert.IsFalse( Directory.Exists( _destinationPath.FullName ) );
            // Add additional assertions to verify that no files/directories were extracted
        }

        [Test]
        [Ignore( "not finished yet" )]
        public async Task ExtractFileAsync_SelfExtractingExe_Success()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            //string archivePath = CreateTemporarySelfExtractingExe( "selfExtracting.exe" );
            //_sourcePaths = new List<string> { archivePath };

            // Act
            bool extractionResult = await new Instruction().ExtractFileAsync( _destinationPath, _sourcePaths );

            // Assert
            Assert.IsTrue( extractionResult );
            // Add more specific assertions if necessary
            Assert.IsTrue( Directory.Exists( _destinationPath.FullName ) );
            // Add additional assertions to validate the extracted files/directories
        }

        [Test]
        public async Task ExtractFileAsync_PermissionDenied_SkipsFile()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            string archivePath = CreateTemporaryArchive( "archiveWithPermissionDenied.zip" );
            _sourcePaths = new List<string> { archivePath };

            // Act
            bool extractionResult = await new Instruction().ExtractFileAsync( _destinationPath, _sourcePaths );

            // Assert
            Assert.IsTrue( extractionResult );
            // Add more specific assertions if necessary
            Assert.IsTrue( Directory.Exists( _destinationPath.FullName ) );
            // Add additional assertions to validate the extracted files/directories and skipped files
        }

        // Helper methods to create temporary archive files

        private string CreateTemporaryArchive( string fileName )
        {
            string archivePath = Path.Combine( Path.GetTempPath(), fileName );
            ZipFile.CreateFromDirectory( "SourceDirectory", archivePath );
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
                    DefaultExtractDirectory = _destinationPath.FullName,
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
