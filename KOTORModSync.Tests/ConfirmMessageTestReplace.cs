// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using KOTORModSync.Core.TSLPatcher;
using NUnit.Framework;


namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ConfirmMessageTestReplace
	{
		private string _testDirectoryPath;

		[SetUp]
		public void SetUp()
		{
			_testDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			_ = Directory.CreateDirectory(_testDirectoryPath);
		}

		[TearDown]
		public void TearDown() => Directory.Delete(_testDirectoryPath, true);

		[Test]
		public void DisableConfirmations_NullDirectory_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => IniHelper.DisableConfirmations(null!));
		}

		[Test]
		public void DisableConfirmations_NoIniFiles_ThrowsInvalidOperationException()
		{
			DirectoryInfo directory = new DirectoryInfo(_testDirectoryPath);

			Assert.Throws<InvalidOperationException>(() => IniHelper.DisableConfirmations(directory));
		}

		[Test]
		public void DisableConfirmations_ConfirmMessageExists_ReplacesWithN_A()
		{
			const string iniFileName = "sample.ini";
			const string content = "[Settings]\nConfirmMessage=suffer the consequences by proceeding. Continue anyway?";

			File.WriteAllText(Path.Combine(_testDirectoryPath, iniFileName), content);

			var directory = new DirectoryInfo(_testDirectoryPath);

			IniHelper.DisableConfirmations(directory);

			string modifiedContent = File.ReadAllText(Path.Combine(_testDirectoryPath, iniFileName));
			Assert.That(modifiedContent, Does.Contain("ConfirmMessage=N/A"));
		}
	}

}
