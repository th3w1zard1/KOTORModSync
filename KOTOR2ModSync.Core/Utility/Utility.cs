using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace KOTOR2ModSync.Core
{
    public static class Utility
    {
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
                Console.WriteLine($"Failed to access files in destination directory: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                Console.WriteLine($"Your pathname is too long: '{directory.FullName}'");
                Console.WriteLine($"Please utilize the registry patch that increases windows legacy pathlimit of 260, or move your KOTOR2 installation to another directory.");
                Console.WriteLine($"Failed to access files in destination directory: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to access files in destination directory: {ex.Message}");
            }
            return false;
        }
        public static async Task WaitForProcessExitAsync(Process process, string processName)
        {
            // Wait for the process to exit
            while (!process.HasExited)
            {
                await Task.Delay(1000); // Wait for 1 second
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
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                // Call the provided 'onExited' method and set the 'isInstallSuccessful' variable accordingly
                isInstallSuccessful = await onExited(process);

                process.Dispose();
            };

            process.Start();

            await Task.Run(() => process.WaitForExit());

            return isInstallSuccessful;
        }
        public static DirectoryInfo ChooseDirectory()
        {
            Console.Write("Enter the path: ");
            string thisPath = Console.ReadLine().Trim();

            if (!Directory.Exists(thisPath))
            {
                Console.WriteLine($"Directory '{thisPath}' does not exist.");
                return null;
            }
            return new DirectoryInfo(thisPath);
        }
    }
}
