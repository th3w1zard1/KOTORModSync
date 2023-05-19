// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core.Utility
{
    internal static class PlatformAgnosticMethods
    {
        public static int CalculateMaxDegreeOfParallelism() {
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


        public static long GetAvailableMemory() {
            long availableMemory = 0;

            // Check if the required command/method exists on the current platform
            if (!TryExecuteCommand("sysctl -n hw.memsize", out string output) &&
                !TryExecuteCommand("free -b", out output) &&
                !TryExecuteCommand("wmic OS get FreePhysicalMemory", out output)) {
                // Platform-agnostic fallback logic for getting available memory
                // Example: Set a default available memory value
                availableMemory = 4L * 1024 * 1024 * 1024; // 4GB

                return availableMemory;
            }

            var regex = new Regex(@"\d+");
            var match = regex.Match(output);
            if (match.Success && match.Groups.Count > 0) {
                long.TryParse(match.Groups[0].Value, out availableMemory);
            }

            return availableMemory;
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

        public static bool TryExecuteCommand(string command, out string output) {
            try {
                var process = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = GetShellExecutable(),
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                using (var reader = process.StandardOutput) {
                    output = reader.ReadToEnd();
                }

                process.WaitForExit();

                return true;
            } catch {
                output = string.Empty;
                return false;
            }
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

        public static string GetShellExecutable() {
            string[] shellExecutables =
            {
                "sh", "/bin/sh", "/usr/bin/sh", "/usr/local/bin/sh",
                "bash", "/bin/bash", "/usr/bin/bash", "/usr/local/bin/bash"
            };

            foreach (string executable in shellExecutables) {
                if (TryExecuteCommand($"command -v {executable}", out _)) {
                    return executable;
                }
            }

            return string.Empty;
        }

    }
}
