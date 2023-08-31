// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using KOTORModSync.Core.FileSystemPathing;

namespace KOTORModSync.Tests
{
	internal class PathCaseSensitivityTests
	{
#pragma warning disable CS8618
		private static string s_testDirectory;
#pragma warning restore CS8618
		private DirectoryInfo _subDirectory = null!;

		private DirectoryInfo _tempDirectory = null!;

		[SetUp]
		public static void InitializeTestDirectory()
		{
			s_testDirectory = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() );
			_ = Directory.CreateDirectory( s_testDirectory );
		}

		[TearDown]
		public static void CleanUpTestDirectory() => Directory.Delete( s_testDirectory, recursive: true );

		[SetUp]
		public void Setup()
		{
			_tempDirectory = new DirectoryInfo( Path.Combine( Path.GetTempPath(), "UnitTestTempDir" ) );
			_tempDirectory.Create();
			_subDirectory = new DirectoryInfo( Path.Combine( _tempDirectory.FullName, "SubDir" ) );
			_subDirectory.Create();
		}

		[TearDown]
		public void TearDown()
		{
			_subDirectory.Delete( true );
			_tempDirectory.Delete( true );
		}

		[Test]
		public void FindCaseInsensitiveDuplicates_ThrowsArgumentNullException_WhenDirectoryIsNull()
		{
			DirectoryInfo? directory = null;
			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			_ = Assert.Throws<ArgumentNullException>( () => PathHelper.FindCaseInsensitiveDuplicates( directory! ).ToList() );
		}


		[Test]
		public void FindCaseInsensitiveDuplicates_ReturnsEmptyList_WhenDirectoryIsEmpty()
		{
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( _tempDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine(string.Join(Environment.NewLine, result.Select(item => item.FullName)));

			Assert.That( result, Is.Empty, $"Expected 0 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void FindCaseInsensitiveDuplicates_ReturnsEmptyList_WhenNoDuplicatesExist()
		{
			// Arrange
			var file1 = new FileInfo( Path.Combine( _tempDirectory.FullName, "file1.txt" ) );
			file1.Create().Close();
			var file2 = new FileInfo( Path.Combine( _tempDirectory.FullName, "file2.txt" ) );
			file2.Create().Close();

			// Act
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( _tempDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine(string.Join(Environment.NewLine, result.Select(item => item.FullName)));

			// Assert
			Assert.That( result, Is.Empty, $"Expected 0 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		// will always fail on windows
		public void FindCaseInsensitiveDuplicates_FindsFileDuplicates_CaseInsensitive()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			// Arrange
			var file1 = new FileInfo( Path.Combine( _tempDirectory.FullName, "file1.txt" ) );
			file1.Create().Close();
			var file2 = new FileInfo( Path.Combine( _tempDirectory.FullName, "FILE1.txt" ) );
			file2.Create().Close();

			// Act
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( _tempDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine(string.Join(Environment.NewLine, result.Select(item => item.FullName)));

			// Assert
			Assert.That( result.ToList(), Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void FindCaseInsensitiveDuplicates_IgnoresNonDuplicates()
		{
			// Arrange
			var file1 = new FileInfo( Path.Combine( _tempDirectory.FullName, "file1.txt" ) );
			file1.Create().Close();
			var file2 = new FileInfo( Path.Combine( _subDirectory.FullName, "file2.txt" ) );
			file2.Create().Close();

			// Act
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( _tempDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine(string.Join(Environment.NewLine, result.Select(item => item.FullName)));

			// Assert
			Assert.That( result, Is.Empty, $"Expected 0 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void FindCaseInsensitiveDuplicates_IgnoresExtensions()
		{
			// Arrange
			var file1 = new FileInfo( Path.Combine( _tempDirectory.FullName, "file1.txt" ) );
			file1.Create().Close();
			var file2 = new FileInfo( Path.Combine( _subDirectory.FullName, "FILE1.png" ) );
			file2.Create().Close();

			// Act
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( _tempDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			// Assert
			Assert.That( result, Is.Empty, $"Expected 0 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestGetClosestMatchingEntry()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			string file1 = Path.Combine( s_testDirectory, "file.txt" );
			string file2 = Path.Combine( s_testDirectory, "FILE.TXT" );
			File.WriteAllText( file1, contents: "Test content" );
			File.WriteAllText( file2, contents: "Test content" );
			Assert.Multiple( () =>
			{
				Assert.That( PathHelper.GetCaseSensitivePath( Path.Combine( Path.GetDirectoryName( file1 )!, Path.GetFileName( file1 ).ToUpperInvariant() ) ).Item1, Is.EqualTo( file2 ) );
				Assert.That( PathHelper.GetCaseSensitivePath( file1.ToUpperInvariant() ).Item1, Is.EqualTo( file2 ) );
			} );
		}

		[Test]
		public void TestDuplicatesWithFileInfo()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			File.WriteAllText( Path.Combine( s_testDirectory, "file.txt" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( s_testDirectory, "File.txt" ), contents: "Test content" );

			var fileInfo = new FileInfo( Path.Combine( s_testDirectory, "file.txt" ) );
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( fileInfo ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestDuplicatesWithDirectoryNameString()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			File.WriteAllText( Path.Combine( s_testDirectory, "file.txt" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( s_testDirectory, "File.txt" ), contents: "Test content" );

			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( s_testDirectory ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestDuplicateDirectories()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			_ = Directory.CreateDirectory( Path.Combine( s_testDirectory, "subdir" ) );
			_ = Directory.CreateDirectory( Path.Combine( s_testDirectory, "SubDir" ) );

			var dirInfo = new DirectoryInfo( s_testDirectory );
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( dirInfo ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestDuplicatesWithDifferentCasingFilesInNestedDirectories()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			string subDirectory = Path.Combine( s_testDirectory, "SubDirectory" );
			_ = Directory.CreateDirectory( subDirectory );

			File.WriteAllText( Path.Combine( s_testDirectory, "file.txt" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( s_testDirectory, "file.TXT" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( subDirectory, "FILE.txt" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( subDirectory, "file.tXT" ), contents: "Test content" );

			var dirInfo = new DirectoryInfo( s_testDirectory );
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( dirInfo, includeSubFolders: true ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			Assert.That( result, Has.Count.EqualTo( 4 ), $"Expected 4 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestDuplicateNestedDirectories()
		{
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				Console.WriteLine( "Test is not possible on Windows." );
				return;
			}

			string subDir1 = Path.Combine( s_testDirectory, "SubDir" );
			string subDir2 = Path.Combine( s_testDirectory, "subdir" );

			_ = Directory.CreateDirectory( subDir1 );
			_ = Directory.CreateDirectory( subDir2 );

			File.WriteAllText( Path.Combine( subDir1, "file.txt" ), contents: "Test content" );
			File.WriteAllText( Path.Combine( subDir2, "file.txt" ), contents: "Test content" );

			var dirInfo = new DirectoryInfo( s_testDirectory );
			List<FileSystemInfo> result = PathHelper.FindCaseInsensitiveDuplicates( dirInfo, includeSubFolders: true ).ToList();

			var failureMessage = new StringBuilder();
			failureMessage.AppendLine( string.Join( Environment.NewLine, result.Select( item => item.FullName ) ) );

			Assert.That( result, Has.Count.EqualTo( 2 ), $"Expected 2 items, but found {result.Count}. Output: {failureMessage}" );
		}

		[Test]
		public void TestInvalidPath()
		{
			_ = Assert.Throws<ArgumentException>(
				// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
				() => PathHelper.FindCaseInsensitiveDuplicates( "Invalid>Path" )?.ToList()
			);
		}

		[Test]
		public void GetCaseSensitivePath_ValidFile_ReturnsSamePath()
		{
			// Arrange
			string testFilePath = Path.Combine( s_testDirectory, "test.txt" );
			File.Create( testFilePath ).Close();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( testFilePath, isFile: true ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( testFilePath ) );
		}

		[Test]
		public void GetCaseSensitivePath_ValidDirectory_ReturnsSamePath()
		{
			// Arrange
			string testDirPath = Path.Combine( s_testDirectory, "testDir" );
			_ = Directory.CreateDirectory( testDirPath );

			// Act
			DirectoryInfo? result = PathHelper.GetCaseSensitivePath( new DirectoryInfo( testDirPath ) );

			// Assert
			Assert.That( result.FullName, Is.EqualTo( testDirPath ) );
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
		public void GetCaseSensitivePath_InvalidCharactersInPath_ReturnsOriginalPath()
		{
			// Arrange
			string fileName = "invalid>path";
			string invalidPath = Path.Combine( s_testDirectory, fileName );
			string upperCasePath = invalidPath.ToUpperInvariant();

			// Act & Assert
			(string, bool?) result = PathHelper.GetCaseSensitivePath( upperCasePath );
			Assert.Multiple( () =>
			{
				Assert.That( result.Item1, Is.EqualTo( Path.Combine( s_testDirectory, fileName.ToUpperInvariant() ) ) );
				Assert.That( result.Item2, Is.Null );
			} );
		}

		[Test]
		public void GetCaseSensitivePath_RelativePath_ReturnsAbsolutePath()
		{
			// Arrange
			string testFilePath = Path.Combine( s_testDirectory, "test.txt" );
			File.Create( testFilePath ).Close();
			string relativePath = Path.GetRelativePath( Directory.GetCurrentDirectory(), testFilePath );
			string upperCasePath = relativePath.ToUpperInvariant();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( upperCasePath ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( testFilePath ) );
		}


		[Test]
		public void GetCaseSensitivePath_EntirePathCaseIncorrect_ReturnsCorrectPath()
		{
			// Arrange
			string testFilePath = Path.Combine( s_testDirectory, "test.txt" );
			File.Create( testFilePath ).Close();
			string upperCasePath = testFilePath.ToUpperInvariant();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( upperCasePath, isFile: true ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( testFilePath ) );
		}

		[Test]
		public void GetCaseSensitivePath_NonExistentFile_ReturnsCaseSensitivePath()
		{
			// Arrange
			string nonExistentFileName = "non_existent_file.txt";
			string nonExistentFilePath = Path.Combine( s_testDirectory, nonExistentFileName );
			string upperCasePath = nonExistentFilePath.ToUpperInvariant();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( upperCasePath ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( Path.Combine( s_testDirectory, nonExistentFileName.ToUpperInvariant() ) ) );
		}

		[Test]
		public void GetCaseSensitivePath_NonExistentDirAndChildFile_ReturnsCaseSensitivePath()
		{
			// Arrange
			string nonExistentRelFilePath = Path.Combine( "non_existent_dir", "non_existent_file.txt" );
			string nonExistentFilePath = Path.Combine( s_testDirectory, nonExistentRelFilePath );
			string upperCasePath = nonExistentFilePath.ToUpperInvariant();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( upperCasePath, isFile: true ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( Path.Combine( s_testDirectory, nonExistentRelFilePath.ToUpperInvariant() ) ) );
		}

		[Test]
		public void GetCaseSensitivePath_NonExistentDirectory_ReturnsCaseSensitivePath()
		{
			// Arrange
			string nonExistentRelPath = Path.Combine( "non_existent_dir", "non_existent_child_dir" );
			string nonExistentDirPath = Path.Combine( s_testDirectory, nonExistentRelPath );
			string upperCasePath = nonExistentDirPath.ToUpperInvariant();

			// Act
			string? result = PathHelper.GetCaseSensitivePath( upperCasePath ).Item1;

			// Assert
			Assert.That( result, Is.EqualTo( Path.Combine( s_testDirectory, nonExistentRelPath.ToUpperInvariant() ) ) );
		}
	}
}
