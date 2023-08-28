// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.FileSystemPathing;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class WildcardEnumerationTests
    {
	    private readonly string _basePath = Path.Combine( Path.GetTempPath(), path2: "tsl mods" );

	    [Test]
        public void EnumerateFilesWithWildcards_Should_ReturnMatchingFiles()
        {
            // Create test directories and files
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Korriban High Resolution-TPC Version-1",
                    path3: "Korriban HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Korriban High Resolution-TPC Version-2",
                    path3: "Korriban HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360",
                    path3: "Malachor V HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361",
                    path3: "Malachor V HR",
                    path4: "Override"
                )
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-1",
                    "Korriban HR",
                    "Override",
                    "file1.txt"
                ),
                contents: "Content 1"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-2",
                    "Korriban HR",
                    "Override",
                    "file2.txt"
                ),
                contents: "Content 2"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360",
                    "Malachor V HR",
                    "Override",
                    "file3.txt"
                ),
                contents: "Content 3"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361",
                    "Malachor V HR",
                    "Override",
                    "file4.txt"
                ),
                contents: "Content 4"
            );

            var pathsToTest = new List<string>
            {
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-1",
                    "Korriban HR",
                    "Override",
                    "file1.txt"
                ),
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution*TPC Version*",
                    "Korriban HR",
                    "Override",
                    "*"
                ),
                Path.Combine( _basePath, "*", "Korriban HR", "Override", "file2.txt" ),
                Path.Combine( _basePath, "*", "*", "Override", "file3.txt" ),
                Path.Combine( _basePath, "*", "*", "Override", "*" ),
                Path.Combine( _basePath, "*", "*", "*", "file4.txt" ),
                Path.Combine( _basePath, "*", "*", "*", "*" ),
                Path.Combine( _basePath, "*Korriban High Resolution*", "Korriban HR", "Override", "*" ),
                Path.Combine(
                    _basePath,
                    "*Malachor V High Resolution - TPC Version-*",
                    "Malachor V HR",
                    "Override",
                    "*"
                ),
                Path.Combine( _basePath, "*", "Malachor V HR", "*", "file4.txt" ),
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-*",
                    "Korriban HR",
                    "Override",
                    "*"
                ),
                Path.Combine( _basePath, "Ultimate * High Resolution*TPC Version-*", "*", "Override", "*" ),
                Path.Combine( _basePath, "Ultimate * High Resolution-TPC Version-*", "*", "Override", "*" ),
            };

            foreach ( string path in pathsToTest )
            {
                List<string> paths = new()
                {
                    path,
                };
                List<string> files = PathHelper.EnumerateFilesWithWildcards( paths );
                Console.WriteLine( $"Files found for path: {path}" );
                foreach ( string? file in files )
                {
                    Console.WriteLine( file );
                }

                Assert.That( files, Is.Not.Empty, $"No files found for path: {path}" );
            }
        }

        [Test]
        public void EnumerateFilesWithWildcards_ShouldNotReturnFiles()
        {
            // Create test directories and files
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Korriban High Resolution-TPC Version-1",
                    path3: "Korriban HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Korriban High Resolution-TPC Version-2",
                    path3: "Korriban HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360",
                    path3: "Malachor V HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361",
                    path3: "Malachor V HR",
                    path4: "Override"
                )
            );
            _ = Directory.CreateDirectory(
                Path.Combine(
                    _basePath,
                    path2: "Ultimate_Robes_Repair_For_TSL_v1.1",
                    path3: "Ultimate_Robes_Repair_For_TSL_v1.1",
                    path4: "TSLRCM backup"
                )
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-1",
                    "Korriban HR",
                    "Override",
                    "file1.txt"
                ),
                contents: "Content 1"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution-TPC Version-2",
                    "Korriban HR",
                    "Override",
                    "file2.txt"
                ),
                contents: "Content 2"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Malachor V High Resolution - TPC Version-1106-1-1-1670682360",
                    "Malachor V HR",
                    "Override",
                    "file3.txt"
                ),
                contents: "Content 3"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate Malachor V High Resolution - TPC Version-2107-1-1-1670682361",
                    "Malachor V HR",
                    "Override",
                    "file4.txt"
                ),
                contents: "Content 4"
            );
            File.WriteAllText(
                Path.Combine(
                    _basePath,
                    "Ultimate_Robes_Repair_For_TSL_v1.1",
                    "Ultimate_Robes_Repair_For_TSL_v1.1",
                    "TSLRCM backup",
                    "file5.txt"
                ),
                contents: "Content 5"
            );

            var pathsToTest = new List<string>
            {
                Path.Combine(
                    _basePath,
                    "Ultimate Mal*or High Resolution-TPC Version-1",
                    "Korriban HR",
                    "Override",
                    "file1.txt"
                ),
                Path.Combine(
                    _basePath,
                    "Ultimate Korriban High Resolution*TGA Version*",
                    "Korriban HR",
                    "Override",
                    "file2.txt"
                ),
                Path.Combine( _basePath, "Ultimate * High Resolution-TPC Version-*", "*", "Override", "*", "*" ),
                Path.Combine(
                    _basePath,
                    path2: "Ultimate_Robes_Repair_For_TSL*",
                    path3: "Ultimate_Robes_Repair_For_TSL*",
                    path4: "*.*"
                ),
            };

            foreach ( string path in pathsToTest )
            {
                List<string> files = PathHelper.EnumerateFilesWithWildcards(
                    new List<string>
                    {
                        path,
                    }
                );
                Assert.That( files, Is.Empty, $"Files found for path: {path}" );
            }
        }
    }
}
