// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Exception = System.Exception;

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
                Parallel.Invoke(() => maxParallelism = Math.Max(1, maxParallelism / 2));

            Task<double> maxDiskSpeedTask = Task.Run(() => GetMaxDiskSpeed(Path.GetPathRoot(thisDir.FullName)));
            double maxDiskSpeed = await maxDiskSpeedTask;

            const double diskSpeedThreshold = 100.0; // MB/sec threshold
            if (maxDiskSpeed < diskSpeedThreshold)
                maxParallelism = Math.Max(1, maxParallelism / 2); // Reduce parallelism by half if disk speed is below the threshold

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
                    result = TryExecuteCommand("wmic OS get FreePhysicalMemory");
            }

            if (!result.Success)
                return 4L * 1024 * 1024 * 1024; // 4GB

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
                if (long.TryParse(matchedValue, out long availableMemory))
                    return availableMemory;

            // Platform-agnostic fallback logic for getting available memory
            return 4L * 1024 * 1024 * 1024; // 4GB
        }

        public static (bool Success, string Output, string Error) TryExecuteCommand(string command, int timeoutSeconds = 10)
        {
            string shellPath = GetShellExecutable();
            if (string.IsNullOrEmpty(shellPath))
                return (false, string.Empty, "Unable to retrieve shell executable path.");

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

        private static bool IsShellExecutionSupported()
        {
            string shellExecutable = GetShellExecutable();

            // Check if a valid shell executable was found
            bool isSupported = !string.IsNullOrEmpty(shellExecutable);

            return isSupported;
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
                return executable;

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
                throw new PlatformNotSupportedException("Disk performance checking is not supported on this platform.");

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
            if (!match.Success || match.Groups.Count < 3)
                return maxSpeed;

            string speedString = match.Groups[1].Value;
            string unit = match.Groups[2].Value.ToLower();

            if (!double.TryParse(speedString.Replace(",", ""), out double speed))
                return maxSpeed;

            switch (unit)
            {
                // Convert bytes/sec to MB/sec
                case "bytes/sec": return speed / 1048576;
                case "mb/sec": return speed;
                default: return maxSpeed;
            }
        }

        private static List<ProcessStartInfo> GetProcessStartInfos([CanBeNull] FileInfo programFile, [CanBeNull] string cmdlineArgs, bool noAdmin) => new List<ProcessStartInfo>
        {
            // top-level, preferred startinfo args. Provides the most flexibility with our code.
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                ErrorDialog = false,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsHighest" }
            },
            // perhaps the error dialog was the problem.
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsHighest" }
            },
            // if it's not a console app or command, it needs a window.
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsHighest" }
            },
            // try without redirecting output)
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsHighest" }
            },
            // try using native shell (doesn't support output redirection, perhaps they need admin)
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = IsShellExecutionSupported() // not supported on all OS's.
            },
            // try using RunAsInvoker
            new ProcessStartInfo { FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                EnvironmentVariables =  { ["__COMPAT_LAYER"] = "RunAsInvoker"
            }
        },
        };

        private static async Task HandleProcessExitedAsync([CanBeNull] Process process, [CanBeNull] TaskCompletionSource<int> tcs)
        {
            if (tcs == null)
                throw new ArgumentNullException(nameof(tcs));

            if (process == null)
            {
                // Process disposed of early?
                Logger.LogException(new NotSupportedException());
                tcs.SetResult(-4);
                return;
            }

            string output = process.StartInfo.RedirectStandardOutput
                ? await process.StandardOutput.ReadToEndAsync()
                : null;
            string error = process.StartInfo.RedirectStandardError
                ? await process.StandardError.ReadToEndAsync()
                : null;

            if (process.ExitCode == 0 && error == null)
            {
                tcs.SetResult(process.ExitCode);
                return;
            }

            string logMessage = string.Empty;
            if (process.ExitCode != 0)
                logMessage += $"Process failed with exit code {process.ExitCode}. ";

            StringBuilder logBuilder = new StringBuilder(logMessage);

            if (output != null) logBuilder.Append("Output: ").AppendLine(output);
            if (error != null) logBuilder.Append("Error: ").AppendLine(error);

            Logger.Log(logBuilder.ToString());

            // Process had an error of some sort even though ExitCode is 0?
            tcs.SetResult(-3);
        }


        public static async Task<int> ExecuteProcessAsync(
            [CanBeNull] FileInfo programFile,
            [CanBeNull] string cmdlineArgs,
            bool noAdmin = false
        )
        {
            if (cmdlineArgs == null)
                throw new ArgumentNullException(nameof(cmdlineArgs));

            if (programFile == null)
                throw new ArgumentNullException(nameof(programFile));

            List<ProcessStartInfo> processStartInfosWithFallbacks = GetProcessStartInfos(
                programFile,
                cmdlineArgs,
                noAdmin
            );

            Exception ex = new Exception();
            bool startedProcess = false;
            foreach (ProcessStartInfo startInfo in processStartInfosWithFallbacks)
                try
                {
                    // K1CP can take ages to install, set the timeout time to an hour.
                    using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3600)))
                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;

                        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

                        process.Exited += async (sender, args) => await HandleProcessExitedAsync((Process)sender, tcs);

                        // Handle cancellation using CancellationToken
                        Process localProcess = process;
                        _ = cancellationTokenSource.Token.Register(() =>
                        {
                            if (localProcess.HasExited) return;

                            if (!localProcess.CloseMainWindow())
                                localProcess.Kill();
                            tcs.TrySetCanceled();
                        });

                        process.EnableRaisingEvents = true;
                        process.Start();

                        startedProcess = true;

                        await tcs.Task;

                        if (cancellationTokenSource.Token.IsCancellationRequested)
                            throw new TimeoutException("Process timed out");

                        return tcs.Task.Result;
                    }
                }
                catch (Win32Exception localException)
                {
                    if(!MainConfig.DebugLogging)
                        continue;
                    Logger.Log($"Exception occurred for startInfo: {startInfo}");
                    Logger.LogException(localException);
                    ex = localException;
                }
                catch(Exception ex2)
                {
                    Logger.Log("An unplanned error has occured trying to run {programFile.Name}.");
                    Logger.LogException(ex2);
                    return -6;
                }

            if (startedProcess) return -2; // todo: figure out what scenario this return code will happen in.

            Logger.Log("Process failed to start with all possible combinations of arguments.");
            Logger.LogException(ex);
            return -1;
        }
    }
}