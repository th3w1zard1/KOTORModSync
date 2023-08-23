// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemPathing;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.ConsoleApp
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    internal static class Program
    {
        private static MainConfig MainConfigInstance;

        private static void Main( string[] args )
        {
            try
            {
                if ( args is null )
                {
                    throw new ArgumentNullException( nameof( args ) );
                }

                for ( int index = 0; index < args.Length; index++ )
                {
                    if ( index == 0 )
                    {
                        Console.WriteLine( $"KOTORModSync CLI called with {args.Length} arguments." );
                    }

                    string arg = args[index];
                    Console.WriteLine( $"Argument #{index + 1}: {arg}" );
                }

                bool exit = false;
                MainConfigInstance = new MainConfig();

                while ( !exit )
                {
                    //Console.Clear();
                    Console.WriteLine( "Main Menu:" );
                    Console.WriteLine( "1. Choose Directories" );
                    Console.WriteLine( "2. Validate Mod Downloads" );
                    Console.WriteLine( "3. Install Mod Build" );
                    Console.WriteLine( "4. (dev) generate mod directory trees from compressed files." );
                    Console.WriteLine( "5. Generate SHA1 checksums of a KOTOR installation." );
                    Console.WriteLine( "6: Deserialize Reddit source into TOML" );
                    Console.WriteLine( "9. Exit" );
                    Console.Write( "Enter a command: " );

                    string input = Console.ReadLine();
                    char key;

                    switch ( input )
                    {
                        case "1":
                            Console.WriteLine(
                                "In order for this installer to work, you must download all of the mods you'd like to use into one folder."
                            );
                            Console.WriteLine( "VERY IMPORTANT: Do not extract or rename any mod archives." );
                            Console.WriteLine(
                                "Please specify the directory your mods are downloaded in (e.g. \"%UserProfile%\\Documents\\tsl_mods\")"
                            );
                            DirectoryInfo modDownloads = Utility.ChooseDirectory();
                            MainConfigInstance.sourcePath = modDownloads;
                            if ( modDownloads is null )
                            {
                                Console.WriteLine(
                                    "Please try again and choose a valid directory path to your downloaded mods."
                                );
                                break;
                            }

                            FileInfo[] modFiles = modDownloads.GetFilesSafely(
                                    searchPattern: "*.*",
                                    SearchOption.TopDirectoryOnly
                                )
                                .Where( static file => ArchiveHelper.IsArchive( file.FullName ) )
                                .ToArray();

                            if ( modFiles.Length == 0 )
                            {
                                Console.WriteLine(
                                    $"Directory '{modDownloads.FullName}' does not contain any mod archives (*.zip, *.rar, *.7z)."
                                );
                                return;
                            }

                            Console.WriteLine(
                                $"Found {modFiles.Length} mod files in directory '{modDownloads.FullName}':"
                            );
                            foreach ( FileInfo modFile in modFiles )
                            {
                                Console.WriteLine( $"  {modFile.Name}" );
                            }

                            Console.WriteLine(
                                "Please specify the location of your KOTOR2 installation folder (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")"
                            );
                            DirectoryInfo kotorInstallDir = Utility.ChooseDirectory();
                            MainConfigInstance.destinationPath = kotorInstallDir;
                            if ( kotorInstallDir is null )
                            {
                                Console.WriteLine( "Please try again and choose your KOTOR installation dir." );
                                break;
                            }

                            Console.WriteLine( "Set directory paths..." );
                            break;

                        case "2": break;

                        case "3":
                            Console.WriteLine( "(not implemented yet)" );
                            Console.WriteLine( "Press any key to continue" );
                            _ = Console.ReadKey();
                            //Utility.Component.OutputConfigFile(MainConfig.LastOutputDirectory);
                            //Utility.Component.ReadComponentsFromFile(MainConfig.LastOutputDirectory);
                            break;

                        case "4":
                            if ( MainConfig.DestinationPath is null || MainConfig.SourcePath is null )
                            {
                                Console.WriteLine( "Please select your directories first" );
                                break;
                            }

                            if ( MainConfig.LastOutputDirectory != null )
                            {
                                Console.WriteLine( "Use same output path as last time? (y/N)" );
                                key = Console.ReadKey()
                                    .KeyChar;
                                if ( char.ToLower( key ) == 'n' )
                                {
                                    MainConfigInstance.lastOutputDirectory = null;
                                }
                            }

                            if ( MainConfig.LastOutputDirectory is null )
                            {
                                Console.WriteLine( "Please specify the path to the output file." );
                                MainConfigInstance.lastOutputDirectory = Utility.ChooseDirectory();
                            }

                            string outputPath = Path.Combine(
                                MainConfig.LastOutputDirectory.FullName,
                                path2: "mod_tree_output.json"
                            );
                            ArchiveHelper.OutputModTree( MainConfig.SourcePath, outputPath );
                            Console.WriteLine( "Press any key to continue..." );
                            _ = Console.ReadKey();
                            break;

                        case "5":
                            if ( MainConfig.DestinationPath is null )
                            {
                                Console.WriteLine( "Please select your KOTOR2 installation directory first." );
                                break;
                            }

                            if ( MainConfig.LastOutputDirectory != null )
                            {
                                Console.WriteLine( "Use same output path as last time? (y/N)" );
                                key = Console.ReadKey()
                                    .KeyChar;
                                if ( char.ToLower( key ) == 'n' )
                                {
                                    MainConfigInstance.lastOutputDirectory = null;
                                }
                            }

                            if ( MainConfig.LastOutputDirectory is null )
                            {
                                Console.WriteLine( "Please specify the path to the output file." );
                                MainConfigInstance.lastOutputDirectory = Utility.ChooseDirectory();
                            }

                            _ = Path.Combine( MainConfig.LastOutputDirectory.FullName, path2: "kotor_checksums.json" );
                            break;

                        case "6":
                            Console.WriteLine( "Enter the file path of the source text:" );
                            string filePath = Console.ReadLine();

                            if ( File.Exists( filePath ) )
                            {
                                string source = File.ReadAllText( filePath );
                                List<Component> components = ModParser.ParseMods( source );
                                foreach ( Component mod in components )
                                {
                                    Console.WriteLine( $"Name: {mod.Name}" );
                                    Console.WriteLine( $"ModLink: {mod.ModLink}" );
                                    Console.WriteLine( $"Author: {mod.Author}" );
                                    Console.WriteLine( $"Description: {mod.Description}" );
                                    Console.WriteLine( $"Category: {mod.Category}" );
                                    Console.WriteLine( $"Tier: {mod.Tier}" );
                                    Console.WriteLine( $"Non-English Functionality: {mod.NonEnglishFunctionality}" );
                                    Console.WriteLine( $"Installation Method: {mod.InstallationMethod}" );
                                    Console.WriteLine( $"Installation Instructions: {mod.Directions}" );
                                    Console.WriteLine();
                                }

                                Console.WriteLine( "Enter the output directory for parsed_reddit.toml file" );

                                string outPath = Console.ReadLine();
                                Component.OutputConfigFile(
                                    components,
                                    outPath + Path.DirectorySeparatorChar + "parsed_reddit.toml"
                                );
                                Console.WriteLine( $"File saved as 'parsed_reddit.toml' in directory {outPath}" );
                            }
                            else
                            {
                                Console.WriteLine( "Invalid file path!" );
                            }

                            Console.WriteLine( "Press any key to exit." );
                            _ = Console.ReadKey();
                            break;

                        case "7": break;

                        case "8": break;

                        case "9":
                            // Exit the app
                            exit = true;
                            break;

                        default:
                            Console.WriteLine( "Invalid command" );
                            _ = Console.ReadKey();
                            break;
                    }
                }
            }
            catch ( Exception exception )
            {
                Console.WriteLine( exception );
                throw;
            }
        }

        public static void WriteToLegacyConsole( [CanBeNull] string message ) => Console.WriteLine( message );
    }
}
