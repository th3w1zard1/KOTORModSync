// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Utility
{
    public static class PlatformAgnosticMethods
    {
        public static async Task<int> CalculateMaxDegreeOfParallelismAsync() {
            int maxParallelism = Environment.ProcessorCount; // Start with the number of available processors

            // Adjust the maximum parallelism based on other factors such as file sizes, memory usage, available memory, and disk speed
            // Add your custom logic here to fine-tune the parallelism

            // Example: Limit parallelism based on available memory
            long availableMemory = GetAvailableMemory();
            const long memoryThreshold = 2L * 1024 * 1024 * 1024; // 2GB threshold
            if (availableMemory < memoryThreshold) {
                maxParallelism = Math.Max(1, maxParallelism / 2); // Reduce parallelism by half if available memory is below the threshold
            }

            // Example: Limit parallelism based on disk speed
            bool isSSD = IsSSDDrive();
            if (isSSD) {
                maxParallelism = Math.Max(1, maxParallelism * 2); // Double parallelism if the disk is an SSD
            }

            // Platform-agnostic fallback logic
            if (maxParallelism <= 1) {
                // Fallback logic when unable to determine or adjust parallelism
                maxParallelism = Environment.ProcessorCount; // Reset to the default number of available processors
            }

            return maxParallelism;
        }


        public static long GetAvailableMemory()
        {
            // Check if the required command/method exists on the current platform
            // Check if the required command/method exists on the current platform
            var result = TryExecuteCommand("sysctl -n hw.memsize");
            if (!result.Success)
            {
                result = TryExecuteCommand("free -b");
                if (!result.Success)
                {
                    result = TryExecuteCommand("wmic OS get FreePhysicalMemory");
                }
            }


            if (result.Success)
            {
                string output = result.Output;

                // Update the regular expressions for matching memory values
                string[] patterns = {
                    @"\d{1,3}(,\d{3})*",                   // wmic command
                    @"\d+\s+\d+\s+\d+\s+(\d+)",             // free command
                    @"\d+(\.\d+)?",                         // sysctl command
                };

                foreach (string pattern in patterns)
                {
                    Match match = Regex.Match(output, pattern);
                    if (match.Success)
                    {
                        string matchedValue = match.Groups[1].Value.Replace(",", "");
                        if (long.TryParse(matchedValue, out long availableMemory))
                        {
                            return availableMemory;
                        }
                    }
                }
            }

            // Platform-agnostic fallback logic for getting available memory
            return 4L * 1024 * 1024 * 1024; // 4GB
        }


        public static (bool Success, string Output, string Error) TryExecuteCommand(string command, int timeoutSeconds = 10)
        {
            string shellPath = GetShellExecutable();
            if (string.IsNullOrEmpty(shellPath))
            {
                return (false, string.Empty, "Unable to retrieve shell executable path.");
            }

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = shellPath;
                    process.StartInfo.Arguments = $"/c \"{command}\"";  // Use "/c" for Windows command prompt and "-c" for Unix-like shells
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();

                    // Wait for the process to exit or timeout
                    if (!process.WaitForExit(timeoutSeconds * 1000))
                    {
                        process.Kill(); // Terminate the process if it exceeds the timeout
                        return (false, string.Empty, "Command execution timed out.");
                    }

                    string output = process.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    return (true, output, error);
                }
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Command execution failed: {ex.Message}");
            }
        }

        public static string GetShellExecutable()
        {
            string[] shellExecutables =
            {
                "cmd.exe", "powershell.exe", "sh", "/bin/sh", "/usr/bin/sh", "/usr/local/bin/sh",
                "bash", "/bin/bash", "/usr/bin/bash", "/usr/local/bin/bash"
            };

            foreach (string executable in shellExecutables)
            {
                if (File.Exists(executable) || File.Exists(Path.Combine(Environment.SystemDirectory, executable)))
                {
                    return executable;
                }
            }

            return string.Empty;
        }


        public static bool IsSSDDrive() {
            bool isSSD = false;

            // Check if the required method exists on the current platform
            if (!TryInvokeMethod(typeof(DriveInfo), "IsSSD", out object result) &&
                !TryInvokeMethod(typeof(DriveInfo), "GetDriveType", out result)) {
                // Platform-agnostic fallback logic for determining if the disk is an SSD
                // Example: Assume it's not an SSD
                isSSD = false;

                return isSSD;
            }

            if (result is bool boolResult) {
                isSSD = boolResult;
            } else if (result is int driveType) {
                // Example: Assume SSD for drive types indicating flash storage
                isSSD = driveType == 2 || driveType == 3;
            }

            return isSSD;
        }

        public static bool TryInvokeMethod(Type type, string methodName, out object result) {
            try {
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                result = method?.Invoke(null, null);
                return true;
            } catch {
                result = null;
                return false;
            }
        }
    }
}
