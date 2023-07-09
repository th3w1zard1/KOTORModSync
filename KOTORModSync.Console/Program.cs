// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.ConsoleApp
{
    internal static class Program
    {
        [NotNull] public static MainConfig MainConfigInstance = new();

        private static void Main( [NotNull] string[] args )
        {
            try
            {
                while ( true )
                {
                    string command = args[0];
                    if ( args?.Length == 0 )
                    {
                        ShowMenuOptions();
                        Console.WriteLine( "Please choose an option (1-6) or 'Q' to quit:" );
                        ConsoleKeyInfo input = Console.ReadKey();

                        if ( input.Key == ConsoleKey.Q )
                        {
                            break;
                        }

                        command = input.KeyChar.ToString().ToLower();
                    }

                    string destinationPath = null;
                    string modPath = null;

                    for ( int i = 1; i < args.Length; i++ )
                    {
                        if ( i + 1 >= args.Length || args[i + 1] == null )
                        {
                            break;
                        }

                        i++; // Skip the next argument
                        switch ( args[i] )
                        {
                            case "--destination":
                                MainConfigInstance.destinationPath = new DirectoryInfo( args[i + 1] );
                                break;
                            case "--modpath":
                                MainConfigInstance.sourcePath = new DirectoryInfo( args[i + 1] );
                                break;
                            default:
                                i--; // Don't skip the next argument
                                break;
                        }
                    }

                    switch ( command )
                    {
                        case "1":
                        case "setdirectories":
                            ChooseDirectories( args );
                            break;

                        case "2":
                        case "validatemods":
                            PreinstallValidation( args );
                            break;

                        case "3":
                        case "installmodbuild":
                            InstallModBuild( args );
                            break;

                        case "4":
                        case "generatemoddirectorytrees":
                            GenerateModDirectoryTrees( args );
                            break;

                        case "5":
                        case "generatechecksums":
                            GenerateChecksums();
                            break;

                        case "6":
                        case "parsereddit":
                            DeserializeRedditSource( args );
                            break;

                        default:
                            Console.WriteLine( "Invalid command" );
                            break;
                    }

                    // Check if there are more commands to execute
                    if ( args.Length <= 1 || args[1] != "&&" ) continue;

                    string[] remainingArgs = new string[args.Length - 2];
                    Array.Copy( args, 2, remainingArgs, 0, args.Length - 2 );
                    Main( remainingArgs );
                }
            }
            catch ( Exception exception )
            {
                Console.WriteLine( exception );
                throw;
            }
        }

        private static void ShowMenuOptions()
        {
            Console.WriteLine( "KOTOR ModSync" );
            Console.WriteLine( "Menu Options:" );
            Console.WriteLine( "1. Choose Directories" );
            Console.WriteLine( "2. Validate Mod Downloads" );
            Console.WriteLine( "3. Install Mod Build" );
            Console.WriteLine( "4. Generate Mod Directory Trees" );
            Console.WriteLine( "5. Generate Checksums" );
            Console.WriteLine( "6. Deserialize Reddit Markdown" );
        }


        private static void ChooseDirectories( [NotNull] IReadOnlyList<string> args )
        {
            if ( args.Count < 3 )
            {
                Console.WriteLine( "Usage: KOTORModSyncCLI setdirectories [modDirectory] [destinationDirectory]" );
                return;
            }

            string modDirectory = args[1];
            string destinationDirectory = args[2];

            DirectoryInfo modDownloads = new DirectoryInfo( modDirectory );
            MainConfigInstance.sourcePath = modDownloads;
            if ( !modDownloads.Exists )
            {
                Console.WriteLine( "The specified mod directory does not exist." );
                return;
            }

            string[] modFiles = Directory.GetFiles(
                    modDownloads.FullName,
                    "*.*",
                    SearchOption.TopDirectoryOnly
                )
                .Where( static file => ArchiveHelper.IsArchive( file ) )
                .ToArray();

            if ( modFiles.Length == 0 )
            {
                Console.WriteLine( $"Directory '{modDownloads.FullName}' does not contain any mod archives (*.zip, *.rar, *.7z)." );
                return;
            }

            Console.WriteLine( $"Found {modFiles.Length} mod files in directory '{modDownloads.FullName}':" );
            foreach ( string modFile in modFiles )
            {
                Console.WriteLine( $"  {Path.GetFileName( modFile )}" );
            }

            DirectoryInfo kotorInstallDir = new DirectoryInfo( destinationDirectory );
            MainConfigInstance.destinationPath = kotorInstallDir;
            if ( !kotorInstallDir.Exists )
            {
                Console.WriteLine( "The specified destination directory does not exist." );
                return;
            }

            Console.WriteLine( "Directory paths set successfully." );
        }

        private static void PreinstallValidation( [CanBeNull] string[] strings )
        {
            // code for validating mod downloads
            // ...
        }

        private static void InstallModBuild( [CanBeNull] string[] strings )
        {
            Console.WriteLine( "(not implemented yet)" );
            Console.WriteLine( "Press any key to continue" );
            _ = Console.ReadKey();
            //Utility.Component.OutputConfigFile(MainConfig.LastOutputDirectory);
            //Utility.Component.ReadComponentsFromFile(MainConfig.LastOutputDirectory);
        }

        private static void GenerateModDirectoryTrees( [CanBeNull] string[] strings )
        {
            if ( MainConfigInstance.destinationPath == null || MainConfigInstance.sourcePath == null )
            {
                Console.WriteLine( "Please select your directories first" );
                return;
            }

            if ( MainConfigInstance.lastOutputDirectory != null )
            {
                Console.WriteLine( "Use the same output path as last time? (y/N)" );
                char key = Console.ReadKey().KeyChar;
                if ( char.ToLower( key ) == 'n' )
                {
                    MainConfigInstance.lastOutputDirectory = null;
                }
            }

            if ( MainConfigInstance.lastOutputDirectory == null )
            {
                Console.WriteLine( "Please specify the path to the output file." );
                MainConfigInstance.lastOutputDirectory = Utility.ChooseDirectory();
            }

            string outputPath = Path.Combine( MainConfigInstance.lastOutputDirectory.FullName, "modtreeoutput.json" );
            ArchiveHelper.OutputModTree( MainConfigInstance.sourcePath, outputPath );
            Console.WriteLine( "Press any key to continue..." );
            _ = Console.ReadKey();
        }

        private static void GenerateChecksums()
        {
            if ( MainConfigInstance.destinationPath == null )
            {
                Console.WriteLine( "Please select your KOTOR2 installation directory first." );
                return;
            }

            if ( MainConfigInstance.lastOutputDirectory != null )
            {
                Console.WriteLine( "Use the same output path as last time? (y/N)" );
                char key = Console.ReadKey().KeyChar;
                if ( char.ToLower( key ) == 'n' )
                {
                    MainConfigInstance.lastOutputDirectory = null;
                }
            }

            if ( MainConfigInstance.lastOutputDirectory == null )
            {
                Console.WriteLine( "Please specify the path to the output file." );
                MainConfigInstance.lastOutputDirectory = Utility.ChooseDirectory();
            }

            string outputPath = Path.Combine( MainConfigInstance.lastOutputDirectory.FullName, "kotor_checksums.json" );
            // _ = Path.Combine(MainConfig.LastOutputDirectory.FullName, "kotor_checksums.json"); // Why is this line here?
            // It seems like it's not being used.

            // Your code for generating checksums
            // ...

            Console.WriteLine( "Checksums generated successfully." );
        }

        private static void DeserializeRedditSource( [CanBeNull] string[] strings )
        {
            Console.WriteLine( "Enter the file path of the source text:" );
            string filePath = Console.ReadLine();

            if ( File.Exists( filePath ) )
            {
                string source = File.ReadAllText( filePath );
                List<Component> components = ModParser.ParseMods( source );
                foreach ( Component mod in components )
                {
                    Console.WriteLine( $"Name: '{mod.Name}'" );
                    Console.WriteLine( $"ModLink: '{mod.ModLink}'" );
                    Console.WriteLine( $"Author: '{mod.Author}'" );
                    Console.WriteLine( $"Description: '{mod.Description}'" );
                    Console.WriteLine( $"Category: '{mod.Category}'" );
                    Console.WriteLine( $"Tier: '{mod.Tier}'" );
                    Console.WriteLine( $"Non-English Functionality: '{mod.NonEnglishFunctionality}'" );
                    Console.WriteLine( $"Installation Method: '{mod.InstallationMethod}'" );
                    Console.WriteLine( $"Installation Instructions: '{mod.Directions}'" );
                    Console.WriteLine();
                }

                Console.WriteLine( "Enter the output directory for parsed_reddit.toml file:" );
                string outPath = Console.ReadLine();
                string outputFile = Path.Combine( outPath, "parsed_reddit.toml" );
                Component.OutputConfigFile( components, outputFile );
                Console.WriteLine( $"File saved as 'parsed_reddit.toml' in directory {outPath}" );
            }
            else
            {
                Console.WriteLine( "Invalid file path!" );
            }

            Console.WriteLine( "Press any key to exit." );
            _ = Console.ReadKey();
        }


        public static void WriteToLegacyConsole( [CanBeNull] string message ) => Console.WriteLine( message );
    }
}
