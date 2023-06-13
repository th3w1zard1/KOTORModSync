// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
    [Ignore( "not finished" )]
    public class FileExtractorTests
    {
        private Mock<IConfirmationDialogCallback>? _confirmationDialogMock;
        private Mock<IArchive>? _archiveMock;
        private Mock<IReader>? _readerMock;

        [SetUp]
        public void SetUp()
        {
            _confirmationDialogMock = new Mock<IConfirmationDialogCallback>();
            _archiveMock = new Mock<IArchive>();
            _readerMock = new Mock<IReader>();
        }

        [Test]
        public async Task ExtractFileAsync_InvalidArchiveFile_FailsExtraction()
        {
            // Arrange
            var instruction = new Instruction();

            // Use reflection to access private members
            FieldInfo? sourcePathsField = typeof( Instruction ).GetField(
                "sourcePaths",
                BindingFlags.NonPublic | BindingFlags.Instance );
            sourcePathsField?.SetValue( instruction, new List<string>() { "path/to/invalid/file.txt" } );

            FieldInfo? destinationPathField = typeof( Instruction ).GetField(
                "destinationPath",
                BindingFlags.NonPublic | BindingFlags.Instance );
            string tempPath = Path.GetTempPath();
            destinationPathField?.SetValue( instruction, new DirectoryInfo( tempPath ) );

            _ = Directory.CreateDirectory( tempPath );

            _ = _archiveMock.Setup( x => x.ExtractAllEntries() ).Returns( _readerMock.Object );
            _ = _readerMock.Setup( x => x.MoveToNextEntry() ).Returns( false );

            // Act
            bool result = await instruction.ExtractFileAsync( _confirmationDialogMock.Object );

            // Assert
            Assert.IsFalse( result );
        }

        /*[Test]
        public async Task ExtractFileAsync_InvalidArchiveFormat_FailsExtraction()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            var validFilePath = "path/to/valid/archive.zip";
            _confirmationDialogMock.Setup(x => x.Name).Returns("ConfirmationDialog");
            _archiveMock.Setup(x => x.ExtractAllEntries()).Returns(_readerMock.Object);
            _readerMock.Setup(x => x.MoveToNextEntry()).Returns(false);
            ArchiveHelper.Setup(x => x.IsArchive(validFilePath)).Returns(true);
            ArchiveHelper.Setup(x => x.OpenArchive(It.IsAny<Stream>(), validFilePath)).Returns((IArchive)null);

            // Act
            var result = await fileExtractor.ExtractFileAsync(_confirmationDialogMock.Object);

            // Assert
            Assert.IsFalse(result);
            Logger.Verify(x => x.LogException(It.IsAny<InvalidOperationException>()), Times.Once);
        }

        [Test]
        public async Task ExtractFileAsync_DirectoryWithoutWritePermissions_LogsWarning()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            var validArchivePath = "path/to/valid/archive.zip";
            var invalidDirectoryPath = "path/to/invalid/directory/";
            _confirmationDialogMock.Setup(x => x.Name).Returns("ConfirmationDialog");
            _archiveMock.Setup(x => x.ExtractAllEntries()).Returns(_readerMock.Object);
            _readerMock.SetupSequence(x => x.MoveToNextEntry())
                .Returns(true)
                .Returns(false);
            _readerMock.Setup(x => x.Entry.IsDirectory).Returns(true);
            ArchiveHelper.Setup(x => x.IsArchive(validArchivePath)).Returns(true);
            ArchiveHelper.Setup(x => x.OpenArchive(It.IsAny<Stream>(), validArchivePath)).Returns(_archiveMock.Object);
            Logger.Setup(x => x.Log($"Skip file.txt, directory cannot be determined."));

            // Act
            var result = await fileExtractor.ExtractFileAsync(_confirmationDialogMock.Object);

            // Assert
            Assert.IsTrue(result);
            Logger.Verify(x => x.Log($"Skip file.txt, directory cannot be determined."), Times.Once);
        }

        [Test]
        public async Task ExtractFileAsync_SuccessfulExtraction_ReturnsTrue()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            var validArchivePath = "path/to/valid/archive.zip";
            var destinationPath = "path/to/destination/";
            _confirmationDialogMock.Setup(x => x.Name).Returns("ConfirmationDialog");
            _archiveMock.Setup(x => x.ExtractAllEntries()).Returns(_readerMock.Object);
            _readerMock.SetupSequence(x => x.MoveToNextEntry())
                .Returns(true)
                .Returns(false);
            _readerMock.Setup(x => x.Entry.IsDirectory).Returns(false);
            _readerMock.Setup(x => x.Entry.Key).Returns("file.txt");
            ArchiveHelper.Setup(x => x.IsArchive(validArchivePath)).Returns(true);
            ArchiveHelper.Setup(x => x.OpenArchive(It.IsAny<Stream>(), validArchivePath)).Returns(_archiveMock.Object);
            ArchiveHelper.Setup(x => x.DefaultExtractionOptions).Returns(new ExtractionOptions());
            File.Setup(x => x.Exists(destinationPath)).Returns(false);
            Directory.Setup(x => x.Exists(destinationPath)).Returns(false);
            Directory.Setup(x => x.CreateDirectory(destinationPath));
            Logger.Setup(x => x.Log($"Extract file.txt to {destinationPath}"));

            // Act
            var result = await fileExtractor.ExtractFileAsync(_confirmationDialogMock.Object);

            // Assert
            Assert.IsTrue(result);
            Logger.Verify(x => x.Log($"Extract file.txt to {destinationPath}"), Times.Once);
        }

        [Test]
        public async Task ExtractFileAsync_ExceptionDuringExtraction_FailsExtraction()
        {
            // Arrange
            var fileExtractor = new FileExtractor();
            var validArchivePath = "path/to/valid/archive.zip";
            _confirmationDialogMock.Setup(x => x.Name).Returns("ConfirmationDialog");
            _archiveMock.Setup(x => x.ExtractAllEntries()).Returns(_readerMock.Object);
            _readerMock.SetupSequence(x => x.MoveToNextEntry())
                .Returns(true)
                .Returns(false);
            _readerMock.Setup(x => x.Entry.IsDirectory).Returns(false);
            _readerMock.Setup(x => x.Entry.Key).Returns("file.txt");
            ArchiveHelper.Setup(x => x.IsArchive(validArchivePath)).Returns(true);
            ArchiveHelper.Setup(x => x.OpenArchive(It.IsAny<Stream>(), validArchivePath)).Returns(_archiveMock.Object);
            ArchiveHelper.Setup(x => x.DefaultExtractionOptions).Returns(new ExtractionOptions());
            _readerMock.Setup(x => x.WriteEntryToDirectory(It.IsAny<string>(), It.IsAny<ExtractionOptions>())).Throws<Exception>();
            Logger.Setup(x => x.Log($"[Warning] Skipping file 'file.txt' due to lack of permissions."));

            // Act
            var result = await fileExtractor.ExtractFileAsync(_confirmationDialogMock.Object);

            // Assert
            Assert.IsTrue(result);
            Logger.Verify(x => x.Log($"[Warning] Skipping file 'file.txt' due to lack of permissions."), Times.Once);
        }*/
    }
}
