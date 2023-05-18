// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.ConsoleApp
{
    internal static class Program
    {
        public static MainConfig mainConfig;

        private static void Main(string[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            bool exit = false;
            mainConfig = new MainConfig();

            while (!exit)
            {
                //Console.Clear();
                Console.WriteLine("Main Menu:");
                Console.WriteLine("1. Choose Directories");
                Console.WriteLine("2. Validate Mod Downloads");
                Console.WriteLine("3. Install Modbuild");
                Console.WriteLine("4. (dev) generate mod directory trees from compressed files.");
                Console.WriteLine("5. Generate SHA1 checksums of a KOTOR installation.");
                Console.WriteLine("9. Exit");
                Console.Write("Enter a command: ");

                string input = Console.ReadLine();
                string outputPath;
                char key;

                switch (input)
                {
                    case "1":
                        Console.WriteLine("In order for this installer to work, you must download all of the mods you'd like to use into one folder.");
                        Console.WriteLine("VERY IMPORTANT: Do not extract rename any mod archives.");
                        Console.WriteLine("Please specify the directory your mods are downloaded in (e.g. \"%UserProfile%\\Documents\\tslmods\")");
                        DirectoryInfo modDownloads = Utility.ChooseDirectory();
                        if (modDownloads == null)
                        {
                            Console.WriteLine("Please try again and choose a valid directory path to your downloaded mods.");
                            break;
                        }
                        string[] modFiles = Directory.GetFiles(modDownloads.FullName, "*.zip", SearchOption.TopDirectoryOnly);
                        if (modFiles.Length == 0)
                        {
                            Console.WriteLine($"Directory '{modDownloads.FullName}' does not contain any mod files (*.zip,*.rar,*.7z).");
                            return;
                        }
                        Console.WriteLine($"Found {modFiles.Length} mod files in directory '{modDownloads.FullName}':");
                        foreach (string modFile in modFiles)
                        {
                            Console.WriteLine($"  {Path.GetFileName(modFile)}");
                        }
                        Console.WriteLine("Define KOTOR2 now? (y/N)");
                        key = Console.ReadKey().KeyChar;
                        if (char.ToLower(key) == 'n')
                        {
                            break;
                        }
                        Console.WriteLine("Please specify the location of your KOTOR2 installation folder (e.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Knights of the Old Republic II\")");
                        DirectoryInfo kotorInstallDir = Utility.ChooseDirectory();
                        if (kotorInstallDir == null)
                        {
                            Console.WriteLine("Please try again and choose your KOTOR installation dir.");
                            break;
                        }
                        Console.WriteLine("Set directory paths...");
                        mainConfig.UpdateConfig(modDownloads, kotorInstallDir);
                        break;

                    case "2":
                        break;

                    case "3":
                        Console.WriteLine("(not implemented yet)");
                        Console.WriteLine("Press any key to continue");
                        _ = Console.ReadKey();
                        //Utility.Serializer.FileHandler.OutputConfigFile(MainConfig.LastOutputDirectory);
                        //Utility.Serializer.FileHandler.ReadComponentsFromFile(MainConfig.LastOutputDirectory);
                        break;

                    case "4":
                        if (MainConfig.DestinationPath == null || MainConfig.SourcePath == null)
                        {
                            Console.WriteLine("Please select your directories first");
                            break;
                        }
                        if (MainConfig.LastOutputDirectory != null)
                        {
                            Console.WriteLine("Use same output path as last time? (y/N)");
                            key = Console.ReadKey().KeyChar;
                            if (char.ToLower(key) == 'n')
                            {
                                MainConfig.LastOutputDirectory = null;
                            }
                        }
                        if (MainConfig.LastOutputDirectory == null)
                        {
                            Console.WriteLine("Please specify the path to the output file.");
                            MainConfig.LastOutputDirectory = Utility.ChooseDirectory();
                        }
                        outputPath = Path.Combine(MainConfig.LastOutputDirectory.FullName, "modtreeoutput.json");
                        Core.Utility.Serializer.ArchiveHelper.OutputModTree(MainConfig.SourcePath, outputPath);
                        Console.WriteLine("Press any key to continue...");
                        _ = Console.ReadKey();
                        break;

                    case "5":
                        if (MainConfig.DestinationPath == null)
                        {
                            Console.WriteLine("Please select your KOTOR2 installation directory first.");
                            break;
                        }
                        if (MainConfig.LastOutputDirectory != null)
                        {
                            Console.WriteLine("Use same output path as last time? (y/N)");
                            key = Console.ReadKey().KeyChar;
                            if (char.ToLower(key) == 'n')
                            {
                                MainConfig.LastOutputDirectory = null;
                            }
                        }
                        if (MainConfig.LastOutputDirectory == null)
                        {
                            Console.WriteLine("Please specify the path to the output file.");
                            MainConfig.LastOutputDirectory = Utility.ChooseDirectory();
                        }
                        _ = Path.Combine(MainConfig.LastOutputDirectory.FullName, "kotor_checksums.json");
                        break;

                    case "6":
                        break;

                    case "7":
                        break;

                    case "8":
                        break;

                    case "9":
                        // Exit the app
                        exit = true;
                        break;

                    default:
                        Console.WriteLine("Invalid command");
                        _ = Console.ReadKey();
                        break;
                }
            }
        }

        public static void WriteToLegacyConsole(string message) => Console.WriteLine(message);
    }
}
