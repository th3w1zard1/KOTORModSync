﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Utility
{
    public static class PlatformAgnosticMethods
    {
        public static async Task<int> CalculateMaxDegreeOfParallelismAsync(DirectoryInfo thisDir)
        {
            int maxParallelism = Environment.ProcessorCount; // Start with the number of available processors

            long availableMemory = GetAvailableMemory();
            const long memoryThreshold = 2L * 1024 * 1024 * 1024; // 2GB threshold
            if (availableMemory < memoryThreshold)
            {
                Parallel.Invoke(() => maxParallelism = Math.Max(1, maxParallelism / 2));
            }

            Task<double> maxDiskSpeedTask = Task.Run(() => GetMaxDiskSpeed(Path.GetPathRoot(thisDir.FullName)));
            double maxDiskSpeed = await maxDiskSpeedTask;

            const double diskSpeedThreshold = 100.0; // MB/sec threshold
            if (maxDiskSpeed < diskSpeedThreshold)
            {
                maxParallelism = Math.Max(1, maxParallelism / 2); // Reduce parallelism by half if disk speed is below the threshold
            }

            // Platform-agnostic fallback logic
            if (maxParallelism <= 1)
            {
                // Fallback logic when unable to determine or adjust parallelism
                maxParallelism = Environment.ProcessorCount; // Reset to the default number of available processors
            }

            return maxParallelism;
        }

        public static long GetAvailableMemory()
        {
            // Check if the required command/method exists on the current platform
            (bool Success, string Output, string Error) result = TryExecuteCommand("sysctl -n hw.memsize");
            if (!result.Success)
            {
                result = TryExecuteCommand("free -b");
                if (!result.Success)
                {
                    result = TryExecuteCommand("wmic OS get FreePhysicalMemory");
                }
            }

            if (!result.Success)
            {
                return 4L * 1024 * 1024 * 1024; // 4GB
            }

            string output = result.Output;

            // Update the regular expressions for matching memory values
            string[] patterns =
            {
                @"\d{1,3}(,\d{3})*",        // wmic command
                @"\d+\s+\d+\s+\d+\s+(\d+)", // free command
                @"\d+(\.\d+)?"              // sysctl command
            };

            foreach (string matchedValue in from pattern in patterns
                                            select Regex.Match(output, pattern) into match
                                            where match.Success
                                            select match.Groups[1].Value.Replace(",", ""))
            {
                if (long.TryParse(matchedValue, out long availableMemory))
                {
                    return availableMemory;
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
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = shellPath;
                    process.StartInfo.Arguments = $"/c \"{command}\"";  // Use "/c" for Windows command prompt and "-c" for Unix-like shells
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    _ = process.Start();

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

            foreach (string executable in shellExecutables.Where
            (
                executable => File.Exists(executable) || File.Exists
                    (Path.Combine(Environment.SystemDirectory, executable))
                ))
            {
                return executable;
            }

            return string.Empty;
        }

        public static double GetMaxDiskSpeed(string drivePath)
        {
            string command;
            string arguments;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = "cmd.exe";
                arguments = $"/C winsat disk -drive \"{drivePath}\" -seq -read -ramsize 4096";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                command = "dd";
                arguments = $"if={drivePath} bs=1M count=256 iflag=direct";
            }
            else
            {
                throw new PlatformNotSupportedException("Disk performance checking is not supported on this platform.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                _ = process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Extract the relevant information from the output and calculate the max disk speed
                double maxSpeed = ExtractMaxDiskSpeed(output);
                return maxSpeed;
            }
        }

        private static double ExtractMaxDiskSpeed(string output)
        {
            const double maxSpeed = 0.0;

            Regex regex = new Regex(@"([0-9,.]+)\s*(bytes/sec|MB/sec)");
            Match match = regex.Match(output);
            if (!match.Success || match.Groups.Count < 3) { return maxSpeed; }

            string speedString = match.Groups[1].Value;
            string unit = match.Groups[2].Value.ToLower();

            if (!double.TryParse(speedString.Replace(",", ""), out double speed)) { return maxSpeed; }

            switch (unit)
            {
                // Convert bytes/sec to MB/sec
                case "bytes/sec": return speed / 1048576;
                case "mb/sec": return speed;
                default: return maxSpeed;
            }
        }

        public static async Task<bool> ExecuteProcessAsync(
            FileInfo programFile,
            string cmdlineArgs,
            bool noAdmin = false
        )
        {
            // K1CP can take ages to install, set the timeout time to two hours hour.
            using (var cancellationTokenSource
                = new CancellationTokenSource(TimeSpan.FromSeconds(7200)))
            {
                using (var process = new Process())
                {
                    ProcessStartInfo startInfo = process.StartInfo;
                    startInfo.FileName = programFile.FullName;
                    startInfo.Arguments = cmdlineArgs;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                    startInfo.EnvironmentVariables["__COMPAT_LAYER"] = "RUNASHIGHEST";

                    try
                    {
                        if (!process.Start())
                            throw new InvalidOperationException("Failed to start the process.");
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        Logger.Log(ex.Message);
                        Logger.Log(
                            "The above exception is probably normal, it just means you don't have admin or something.");

                        process.StartInfo = new ProcessStartInfo(startInfo.FileName, startInfo.Arguments)
                        {
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            UseShellExecute = startInfo.UseShellExecute,
                            CreateNoWindow = startInfo.CreateNoWindow,
                            EnvironmentVariables = { ["__COMPAT_LAYER"] = "RUNASHIGHEST" }
                        };

                        try
                        {
                            if (!process.Start())
                                throw new InvalidOperationException("Failed to start the process.");
                        }
                        catch (Exception ex2)
                        {
                            Logger.Log(ex2.Message);
                            Logger.Log(
                                "The above exception is probably normal, it just means you don't have admin or something.");
                            process.StartInfo = new ProcessStartInfo(startInfo.FileName, startInfo.Arguments)
                            {
                                RedirectStandardOutput = false,
                                RedirectStandardError = false,
                                UseShellExecute = true,
                                CreateNoWindow = startInfo.CreateNoWindow
                            };
                            if (!process.Start())
                                throw new InvalidOperationException("Failed to start the process.");
                        }
                    }


                    await Task.Run(() => process.WaitForExit(), cancellationTokenSource.Token);

                    if (!process.HasExited)
                    {
                        process.Kill();
                        throw new TimeoutException("Process timed out after 2 hours.");
                    }

                    string output = null;
                    string error = null;
                    if (process.StartInfo.RedirectStandardOutput)
                    {
                        output = await process.StandardOutput.ReadToEndAsync();
                        error = await process.StandardError.ReadToEndAsync();
                    }

                    if (process.ExitCode != 0)
                    {
                        Logger.Log($"Process failed with exit code {process.ExitCode}. Output:\n{output}");
                        return false;
                    }

                    if (output != null || error != null)
                        Logger.Log($"Output: {output}\n Error: {error}\n");
                    return true;
                }
            }
        }
    }
}