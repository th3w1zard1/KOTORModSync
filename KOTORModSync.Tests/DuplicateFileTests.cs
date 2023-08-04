// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    internal class DuplicateFileTests
    {
        private DirectoryInfo _tempDirectory;
        private DirectoryInfo _subDirectory;

        [SetUp]
        public void Setup()
        {
            _tempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "UnitTestTempDir"));
            _tempDirectory.Create();
            _subDirectory = new DirectoryInfo(Path.Combine(_tempDirectory.FullName, "SubDir"));
            _subDirectory.Create();
        }

        [TearDown]
        public void TearDown()
        {
            _subDirectory.Delete(true);
            _tempDirectory.Delete(true);
        }

        [Test]
        public void FindCaseInsensitiveDuplicates_ThrowsArgumentNullException_WhenDirectoryIsNull()
        {
            DirectoryInfo? directory = null;
            _ = Assert.Throws<ArgumentNullException>( () => PathHelper.FindCaseInsensitiveDuplicates( directory! ) );
        }


        [Test]
        public void FindCaseInsensitiveDuplicates_ReturnsEmptyList_WhenDirectoryIsEmpty()
        {
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            Assert.That( result, Is.Empty );
        }

        [Test]
        public void FindCaseInsensitiveDuplicates_ReturnsEmptyList_WhenNoDuplicatesExist()
        {
            // Arrange
            var file1 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file1.txt"));
            file1.Create().Close();
            var file2 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file2.txt"));
            file2.Create().Close();

            // Act
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            // Assert
            Assert.That( result, Is.Empty );
        }

        [Test]
        // will always fail on windows
        public void FindCaseInsensitiveDuplicates_FindsFileDuplicates_CaseInsensitive()
        {
            // Arrange
            var file1 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file1.txt"));
            file1.Create().Close();
            var file2 = new FileInfo(Path.Combine(_tempDirectory.FullName, "FILE1.txt"));
            file2.Create().Close();

            // Act
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            // Assert
            Assert.That( result, Has.Count.EqualTo( 2 ) );
        }

        [Test]
        [Ignore("not necessary")]
        public void FindCaseInsensitiveDuplicates_FindsFolderDuplicates_CaseInsensitive()
        {
            // Arrange
            var file1 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file1.txt"));
            file1.Create().Close();
            var file2 = new FileInfo(Path.Combine(_subDirectory.FullName, "FILE1.txt"));
            file2.Create().Close();

            // Act
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            // Assert
            Assert.That( result, Has.Count.EqualTo( 2 ) );
        }

        [Test]
        public void FindCaseInsensitiveDuplicates_IgnoresNonDuplicates()
        {
            // Arrange
            var file1 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file1.txt"));
            file1.Create().Close();
            var file2 = new FileInfo(Path.Combine(_subDirectory.FullName, "file2.txt"));
            file2.Create().Close();

            // Act
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            // Assert
            Assert.That( result, Is.Empty );
        }

        [Test]
        public void FindCaseInsensitiveDuplicates_IgnoresExtensions()
        {
            // Arrange
            var file1 = new FileInfo(Path.Combine(_tempDirectory.FullName, "file1.txt"));
            file1.Create().Close();
            var file2 = new FileInfo(Path.Combine(_subDirectory.FullName, "FILE1.png"));
            file2.Create().Close();

            // Act
            List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates(_tempDirectory);

            // Assert
            Assert.That( result, Is.Empty );
        }
    }
}
