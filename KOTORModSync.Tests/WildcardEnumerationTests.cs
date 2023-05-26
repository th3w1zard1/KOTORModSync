// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class WildcardEnumerationTests
    {
        private readonly string BasePath = Path.Combine(Path.GetTempPath(), "tsl mods");

        [Test]
        public void EnumerateFilesWithWildcards_Should_ReturnMatchingFiles()
        {

            // Create test directories and files
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-1", "Korriban HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-2", "Korriban HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360", "Malachor V HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361", "Malachor V HR", "Override"));
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-1", "Korriban HR", "Override", "file1.txt"), "Content 1");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-2", "Korriban HR", "Override", "file2.txt"), "Content 2");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360", "Malachor V HR", "Override", "file3.txt"), "Content 3");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361", "Malachor V HR", "Override", "file4.txt"), "Content 4");

            var pathsToTest = new List<string>
            {
                Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-1", "Korriban HR", "Override", "file1.txt"),
                Path.Combine(BasePath, "Ultimate Korriban High Resolution*TPC Version*", "Korriban HR", "Override", "*"),
                Path.Combine(BasePath, "*", "Korriban HR", "Override", "file2.txt"),
                Path.Combine(BasePath, "*", "*", "Override", "file3.txt"),
                Path.Combine(BasePath, "*", "*", "Override", "*"),
                Path.Combine(BasePath, "*", "*", "*", "file4.txt"),
                Path.Combine(BasePath, "*", "*", "*", "*"),
                Path.Combine(BasePath, "*Korriban High Resolution*", "Korriban HR", "Override", "*"),
                Path.Combine(BasePath, "*Malachor V High Resolution - TPC Version-*", "Malachor V HR", "Override", "*"),
                Path.Combine(BasePath, "*", "Malachor V HR", "*", "file4.txt"),
                Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-*", "Korriban HR", "Override", "*"),
                Path.Combine(BasePath, "Ultimate * High Resolution*TPC Version-*", "*", "Override", "*"),
                Path.Combine(BasePath, "Ultimate * High Resolution-TPC Version-*", "*", "Override", "*")
            };

            foreach (string path in pathsToTest)
            {
                List<string> paths = new() { path };
                List<string> files = Serializer.FileHandler.EnumerateFilesWithWildcards(paths);
                Console.WriteLine($"Files found for path: {path}");
                foreach (string? file in files)
                {
                    Console.WriteLine(file);
                }
                Assert.That(files.Any(), $"No files found for path: {path}");
            }

        }

        [Test]
        public void EnumerateFilesWithWildcards_ShouldNotReturnFiles()
        {
            // Create test directories and files
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-1", "Korriban HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-2", "Korriban HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360", "Malachor V HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361", "Malachor V HR", "Override"));
            _ = Directory.CreateDirectory(Path.Combine(BasePath, "Ultimate_Robes_Repair_For_TSL_v1.1", "Ultimate_Robes_Repair_For_TSL_v1.1", "TSLRCM backup"));
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-1", "Korriban HR", "Override", "file1.txt"), "Content 1");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Korriban High Resolution-TPC Version-2", "Korriban HR", "Override", "file2.txt"), "Content 2");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360", "Malachor V HR", "Override", "file3.txt"), "Content 3");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361", "Malachor V HR", "Override", "file4.txt"), "Content 4");
            File.WriteAllText(Path.Combine(BasePath, "Ultimate_Robes_Repair_For_TSL_v1.1", "Ultimate_Robes_Repair_For_TSL_v1.1", "TSLRCM backup", "file5.txt"), "Content 5");

            var pathsToTest = new List<string>
            {
                Path.Combine(BasePath, "Ultimate Mal*or High Resolution-TPC Version-1", "Korriban HR", "Override", "file1.txt"),
                Path.Combine(BasePath, "Ultimate Korriban High Resolution*TGA Version*", "Korriban HR", "Override", "file2.txt"),
                Path.Combine(BasePath, "Ultimate * High Resolution-TPC Version-*", "*", "Override", "*", "*"),
                Path.Combine(BasePath, "Ultimate_Robes_Repair_For_TSL*", "Ultimate_Robes_Repair_For_TSL*", "*.*")
            };

            foreach (string path in pathsToTest)
            {
                List<string> files = Serializer.FileHandler.EnumerateFilesWithWildcards(new List<string> { path });
                Assert.That(files, Is.Empty, $"Files found for path: {path}");
            }
        }

    }
}
