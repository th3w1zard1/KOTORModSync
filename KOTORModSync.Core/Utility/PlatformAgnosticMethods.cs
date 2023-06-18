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

namespace KOTORModSync.Core.Utility
{
    public static class PlatformAgnosticMethods
    {
        public static async Task<int> CalculateMaxDegreeOfParallelismAsync( DirectoryInfo thisDir )
        {
            int maxParallelism = Environment.ProcessorCount; // Start with the number of available processors

            long availableMemory = GetAvailableMemory();
            const long memoryThreshold = 2L * 1024 * 1024 * 1024; // 2GB threshold
            if ( availableMemory < memoryThreshold )
            {
                Parallel.Invoke( () => maxParallelism = Math.Max( 1, maxParallelism / 2 ) );
            }

            Task<double> maxDiskSpeedTask = Task.Run( () => GetMaxDiskSpeed( Path.GetPathRoot( thisDir.FullName ) ) );
            double maxDiskSpeed = await maxDiskSpeedTask;

            const double diskSpeedThreshold = 100.0; // MB/sec threshold
            if ( maxDiskSpeed < diskSpeedThreshold )
            {
                maxParallelism = Math.Max(
                    1,
                    maxParallelism / 2
                ); // Reduce parallelism by half if disk speed is below the threshold
            }

            // Platform-agnostic fallback logic
            if ( maxParallelism <= 1 )
            {
                // Fallback logic when unable to determine or adjust parallelism
                maxParallelism = Environment.ProcessorCount; // Reset to the default number of available processors
            }

            return maxParallelism;
        }

        public static long GetAvailableMemory()
        {
            // Check if the required command/method exists on the current platform
            (int ExitCode, string Output, string Error) result = TryExecuteCommand( "sysctl -n hw.memsize" );
            string command = "sysctl";

            if ( result.ExitCode != 0 )
            {
                result = TryExecuteCommand( "free -b" );
                command = "free";

                if ( result.ExitCode != 0 )
                {
                    result = TryExecuteCommand( "wmic OS get FreePhysicalMemory" );
                    command = "wmic";
                }
            }

            if ( result.ExitCode != 0 )
            {
                return 0; // no memory command found.
            }

            long availableMemory = ParseAvailableMemory( result.Output, command );
            return availableMemory;
        }

        private static long ParseAvailableMemory( string output, string command )
        {
            string pattern = string.Empty;

            switch ( command )
            {
                case "sysctl":
                    pattern = @"\d+(\.\d+)?"; // sysctl command
                    break;
                case "free":
                    pattern = @"Mem:\s+\d+\s+\d+\s+(\d+)"; // free command
                    break;
                case "wmic":
                    pattern = @"\d+"; // wmic command
                    break;
            }

            Match match = Regex.Match( output, pattern );
            if ( match.Success && long.TryParse( match.Value, out long memory ) )
            {
                return memory;
            }

            return 0;
        }


        public static (int ExitCode, string Output, string Error) TryExecuteCommand( string command )
        {
            string shellPath = GetShellExecutable();
            if ( string.IsNullOrEmpty( shellPath ) )
            {
                return (-1, string.Empty, "Unable to retrieve shell executable path.");
            }

            try
            {
                using ( var process = new Process() )
                {
                    string args = RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
                        ? $"/c \"{command}\"" // Use "/c" for Windows command prompt
                        : $"-c \"{command}\""; // Use "-c" for Unix-like shells
                    var shellFileInfo = new FileInfo( shellPath );
                    Task<(int, string, string)> executeProcessTask = ExecuteProcessAsync( shellFileInfo, args );
                    executeProcessTask.Wait();
                    return executeProcessTask.Result;
                }
            }
            catch ( Exception ex )
            {
                return (-2, string.Empty, $"Command execution failed: {ex.Message}");
            }
        }

        private static bool IsShellExecutionSupported()
        {
            string shellExecutable = GetShellExecutable();

            // Check if a valid shell executable was found
            bool isSupported = !string.IsNullOrEmpty( shellExecutable );

            return isSupported;
        }

        public static string GetShellExecutable()
        {
            string[] shellExecutables =
            {
                "cmd.exe", "powershell.exe", "sh", "/bin/sh", "/usr/bin/sh", "/usr/local/bin/sh", "bash",
                "/bin/bash", "/usr/bin/bash", "/usr/local/bin/bash"
            };

            foreach ( string executable in shellExecutables )
            {
                if ( File.Exists( executable ) )
                {
                    return executable;
                }

                string fullExecutablePath = Path.Combine( Environment.SystemDirectory, executable );
                if ( File.Exists( fullExecutablePath ) )
                {
                    return fullExecutablePath;
                }
            }


            return string.Empty;
        }

        public static double GetMaxDiskSpeed( string drivePath )
        {
            try
            {
                string command;
                string arguments;

                if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
                {
                    command = "cmd.exe";
                    arguments = $"/C winsat disk -drive \"{drivePath}\" -seq -read -ramsize 4096";
                }
                else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux )
                         || RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
                {
                    command = "dd";
                    arguments = $"if={drivePath} bs=1M count=256 iflag=direct";
                }
                else
                {
                    throw new PlatformNotSupportedException(
                        "Disk performance checking is not supported on this platform."
                    );
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                using ( var process = new Process() )
                {
                    process.StartInfo = startInfo;
                    _ = process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Extract the relevant information from the output and calculate the max disk speed
                    double maxSpeed = ExtractMaxDiskSpeed( output );
                    return maxSpeed;
                }
            }
            catch ( Exception e )
            {
                Logger.LogException( e );
                throw;
            }
        }

        private static double ExtractMaxDiskSpeed( string output )
        {
            const double maxSpeed = 0.0;

            var regex = new Regex( @"([0-9,.]+)\s*(bytes/sec|MB/sec)" );
            Match match = regex.Match( output );
            if ( !match.Success || match.Groups.Count < 3 )
            {
                return maxSpeed;
            }

            string speedString = match.Groups[1].Value;
            string unit = match.Groups[2].Value.ToLower();

            if ( !double.TryParse( speedString.Replace( ",", "" ), out double speed ) )
            {
                return maxSpeed;
            }

            switch ( unit )
            {
                // Convert bytes/sec to MB/sec
                case "bytes/sec": return speed / 1048576;
                case "mb/sec": return speed;
                default: return maxSpeed;
            }
        }

        private static List<ProcessStartInfo> GetProcessStartInfos
        (
            [NotNull] FileInfo programFile,
            [NotNull] string cmdlineArgs
        ) => new List<ProcessStartInfo>
        {
            // top-level, preferred ProcessStartInfo args. Provides the most flexibility with our code.
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                ErrorDialog = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsInvoker" }
            },
            // perhaps the error dialog was the problem.
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsInvoker" }
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
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsInvoker" }
            },
            // try without redirecting output
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = false,
                EnvironmentVariables = { ["__COMPAT_LAYER"] = "RunAsInvoker" }
            },
            // try using native shell (doesn't support output redirection, perhaps they need admin)
            new ProcessStartInfo
            {
                FileName = programFile.FullName,
                Arguments = cmdlineArgs,
                UseShellExecute = IsShellExecutionSupported() // not supported on all OS's.
            }
        };

        private static async Task HandleProcessExitedAsync
        (
            [CanBeNull] Process process,
            [CanBeNull] TaskCompletionSource<(int, string, string)> tcs
        )
        {
            try
            {
                if ( tcs == null )
                {
                    throw new ArgumentNullException( nameof( tcs ) );
                }

                if ( process == null )
                {
                    // Process disposed of early?
                    await Logger.LogExceptionAsync( new NotSupportedException() );
                    tcs.SetResult( (-4, string.Empty, string.Empty) );
                    return;
                }

                string output = process.StartInfo.RedirectStandardOutput
                    ? await process.StandardOutput.ReadToEndAsync()
                    : null;
                string error = process.StartInfo.RedirectStandardError
                    ? await process.StandardError.ReadToEndAsync()
                    : null;

                if ( process.ExitCode == 0 && string.IsNullOrEmpty( error ) )
                {
                    tcs.SetResult( (process.ExitCode, output, error) );
                    return;
                }

                string logMessage = string.Empty;
                if ( process.ExitCode != 0 )
                {
                    logMessage += $"Process failed with exit code {process.ExitCode}. ";
                }

                var logBuilder = new StringBuilder( logMessage );

                if ( output != null )
                {
                    _ = logBuilder.Append( "Output: " ).AppendLine( output );
                }

                if ( error != null )
                {
                    _ = logBuilder.Append( "Error: " ).AppendLine( error );
                }

                await Logger.LogAsync( logBuilder.ToString() );

                // Process had an error of some sort even though ExitCode is 0?
                tcs.SetResult( (-3, output, error) );
            }
            catch ( Exception e )
            {
                await Logger.LogExceptionAsync( e );
            }
        }

        public static async Task<(int, string, string)> ExecuteProcessAsync
        (
            [CanBeNull] FileInfo programFile,
            string cmdlineArgs = "",
            int timeout = 0,
            bool noAdmin = false
        )
        {
            if ( programFile == null )
            {
                throw new ArgumentNullException( nameof( programFile ) );
            }

            List<ProcessStartInfo> processStartInfosWithFallbacks = GetProcessStartInfos(
                programFile,
                cmdlineArgs
            );

            var ex = new Exception();
            bool startedProcess = false;
            foreach ( ProcessStartInfo startInfo in processStartInfosWithFallbacks )
                try
                {
                    // K1CP can take ages to install, set the timeout time to an hour.
                    using ( var cancellationTokenSource = new CancellationTokenSource( timeout ) )
                    using ( var process = new Process() )
                    {
                        process.StartInfo = startInfo;

                        var tcs = new TaskCompletionSource<(int, string, string)>();

                        process.Exited += async
                            ( sender, args ) => await HandleProcessExitedAsync( (Process)sender, tcs );

                        // Handle cancellation using CancellationToken
                        if ( timeout > 0 )
                        {
                            Process localProcess = process;

                            void Callback()
                            {
                                try
                                {
                                    if ( localProcess.HasExited )
                                    {
                                        return;
                                    }

                                    if ( !localProcess.CloseMainWindow() )
                                    {
                                        localProcess.Kill();
                                    }

                                    _ = tcs.TrySetCanceled();
                                }
                                catch ( Exception cancellationException )
                                {
                                    Logger.LogException( cancellationException );
                                }
                            }

                            _ = cancellationTokenSource.Token.Register( Callback );
                        }

                        process.EnableRaisingEvents = true;

                        // Start the process
                        startedProcess = process.Start();
                        if ( !startedProcess )
                        {
                            throw new InvalidOperationException( "Failed to start the process." );
                        }

                        _ = await tcs.Task;

                        if ( timeout > 0 )
                        {
                            if ( cancellationTokenSource.Token.IsCancellationRequested )
                            {
                                throw new TimeoutException( "Process timed out" );
                            }
                        }

                        return tcs.Task.Result;
                    }
                }
                catch ( Win32Exception localException )
                {
                    if ( !MainConfig.DebugLogging )
                    {
                        continue;
                    }

                    await Logger.LogAsync( $"Exception occurred for startInfo: {startInfo}" );
                    await Logger.LogExceptionAsync( localException );
                    ex = localException;
                }
                catch ( Exception startinfoException )
                {
                    await Logger.LogAsync( $"An unplanned error has occurred trying to run {programFile.Name}." );
                    await Logger.LogExceptionAsync( startinfoException );
                    return (-6, string.Empty, string.Empty);
                }

            if ( startedProcess )
            {
                return
                    (-2, string.Empty,
                        string.Empty); // todo: figure out what scenario this return code will happen in.
            }

            await Logger.LogAsync( "Process failed to start with all possible combinations of arguments." );
            await Logger.LogExceptionAsync( ex );
            return (-1, string.Empty, string.Empty);
        }
    }
}
