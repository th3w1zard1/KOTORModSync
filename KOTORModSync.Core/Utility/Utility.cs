// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Utility
{
    public static class Utility
    {
        public interface IConfirmationDialogCallback
        {
            Task<bool> ShowConfirmationDialog(string message);
        }
        public static string ReplaceCustomVariables(string path)
        {
            string normalizedPath = path.Replace("<<modDirectory>>", MainConfig.SourcePath.FullName)
                                       .Replace("<<kotorDirectory>>", MainConfig.DestinationPath.FullName);
            return Path.GetFullPath(normalizedPath);
        }
        public static bool CanWriteToDirectory(DirectoryInfo directory)
        {
            try
            {
                // Attempt to create a file in the directory
                string fileName = Path.Combine(directory.FullName, Path.GetRandomFileName());
                using (FileStream fs = File.Create(fileName)) { }
                File.Delete(fileName); // Clean up the file
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log($"Failed to access files in destination directory: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                Logger.LogException(ex);
                Logger.Log($"Your pathname is too long: '{directory.FullName}'");
                Logger.Log("Please utilize the registry patch that increases windows legacy pathlimit of 260, or move your KOTOR2 installation to a shorter directory.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to access files in destination directory: {ex.Message}");
            }
            return false;
        }

        public static async Task WaitForProcessExitAsync(Process process, string processName)
        {
            // Wait for the process to exit
            while (!process.HasExited)
            {
                await Task.Delay(1000); // 1 second is the recommended default
            }

            // Make sure the process exited correctly
            if (process.ExitCode != 0)
            {
                throw new Exception($"The process {processName} exited with code {process.ExitCode}.");
            }
        }

        public static async Task<bool> ExecuteProcessAsync(string fileName, string arguments, Func<Process, Task<bool>> onExited)
        {
            var process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            bool isInstallSuccessful = false;

            process.EnableRaisingEvents = true;

            process.Exited += async (sender, e) =>
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                // Call the provided 'onExited' method and set the 'isInstallSuccessful' variable accordingly
                isInstallSuccessful = await onExited(process);

                process.Dispose();
            };

            _ = process.Start();

            await Task.Run(() => process.WaitForExit());

            return isInstallSuccessful;
        }

        public static DirectoryInfo ChooseDirectory()
        {
            Console.Write("Enter the path: ");
            string thisPath = Console.ReadLine().Trim();

            if (!Directory.Exists(thisPath))
            {
                Logger.Log($"Directory '{thisPath}' does not exist.");
                return null;
            }
            return new DirectoryInfo(thisPath);
        }
    }
}
