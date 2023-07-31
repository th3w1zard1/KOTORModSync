// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class PlatformAgnosticMethods
    {
        // Overload for a string representation of the folder path.

        public static async Task<int> CalculateMaxDegreeOfParallelismAsync( [CanBeNull] DirectoryInfo thisDir )
        {
            int maxParallelism = Environment.ProcessorCount; // Start with the number of available processors

            long availableMemory = GetAvailableMemory();
            const long memoryThreshold = 2L * 1024 * 1024 * 1024; // 2GB threshold
            if ( availableMemory < memoryThreshold )
            {
                Parallel.Invoke( () => maxParallelism = Math.Max( val1: 1, maxParallelism / 2 ) );
            }

            var maxDiskSpeedTask = Task.Run( () => GetMaxDiskSpeed( Path.GetPathRoot( thisDir.FullName ) ) );
            double maxDiskSpeed = await maxDiskSpeedTask;

            const double diskSpeedThreshold = 100.0; // MB/sec threshold
            if ( maxDiskSpeed < diskSpeedThreshold )
            {
                maxParallelism = Math.Max(
                    val1: 1,
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

            return result.ExitCode != 0
                ? 0 // no memory command found.
                : ParseAvailableMemory( result.Output, command );
        }

        private static long ParseAvailableMemory( [NotNull] string output, [NotNull] string command )
        {
            if ( string.IsNullOrWhiteSpace( output ) )
            {
                throw new ArgumentException(
                    message: "Value cannot be null or whitespace.",
                    nameof( output )
                );
            }

            if ( string.IsNullOrWhiteSpace( command ) )
                throw new ArgumentException( message: "Value cannot be null or whitespace.", nameof( command ) );

            string pattern = string.Empty;
            switch ( command.ToLowerInvariant() )
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
            return match.Success && long.TryParse( match.Value, out long memory )
                ? memory
                : 0;
        }

        public static (int ExitCode, string Output, string Error) TryExecuteCommand( [CanBeNull] string command )
        {
            string shellPath = GetShellExecutable();
            if ( string.IsNullOrEmpty( shellPath ) )
                return ( -1, string.Empty, "Unable to retrieve shell executable path." );

            try
            {
                using ( var process = new Process() )
                {
                    string args = RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
                        ? $"/c \"{command}\""  // Use "/c" for Windows command prompt
                        : $"-c \"{command}\""; // Use "-c" for Unix-like shells
                    var shellFileInfo = new FileInfo( shellPath );
                    Task<(int, string, string)> executeProcessTask = ExecuteProcessAsync( shellFileInfo, args );
                    executeProcessTask.Wait();
                    return executeProcessTask.Result;
                }
            }
            catch ( Exception ex )
            {
                return ( -2, string.Empty, $"Command execution failed: {ex.Message}" );
            }
        }

        private static bool IsShellExecutionSupported()
        {
            string shellExecutable = GetShellExecutable();

            // Check if a valid shell executable was found
            bool isSupported = !string.IsNullOrEmpty( shellExecutable );

            return isSupported;
        }

        [CanBeNull]
        public static string GetShellExecutable()
        {
            string[] shellExecutables =
            {
                "cmd.exe", "powershell.exe", "sh", "bash", "/bin/sh", "/usr/bin/sh", "/usr/local/bin/sh", "/bin/bash",
                "/usr/bin/bash", "/usr/local/bin/bash",
            };

            foreach ( string executable in shellExecutables )
            {
                if ( File.Exists( executable ) )
                {
                    return executable;
                }

                if ( executable == null )
                    throw new NullReferenceException( nameof( executable ) );

                string fullExecutablePath = Path.Combine( Environment.SystemDirectory, executable );
                if ( File.Exists( fullExecutablePath ) )
                {
                    return fullExecutablePath;
                }
            }

            return string.Empty;
        }

        public static double GetMaxDiskSpeed( [CanBeNull] string drivePath )
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
                    FileName = command, Arguments = arguments, RedirectStandardOutput = true, UseShellExecute = false,
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

            string speedString = match.Groups[1]
                .Value;
            string unit = match.Groups[2]
                .Value.ToLower();

            if ( !double.TryParse( speedString.Replace( oldValue: ",", newValue: "" ), out double speed ) )
            {
                return maxSpeed;
            }

            switch ( unit )
            {
                // Convert bytes/sec to MB/sec
                case "bytes/sec": return speed / 1024 / 1024;
                case "mb/sec": return speed;
                default: return maxSpeed;
            }
        }

        public static bool? IsExecutorAdmin()
        {
            if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
            {
                // Check for administrator privileges on Windows
                var windowsIdentity = WindowsIdentity.GetCurrent();
                var windowsPrincipal = new WindowsPrincipal( windowsIdentity );
                return windowsPrincipal.IsInRole( WindowsBuiltInRole.Administrator );
            }

            // Unsupported platform
            if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux )
                && !RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
            {
                return null;
            }

            // Check for root privileges on Linux and macOS
            try
            {
                // Try to load the libc library and call geteuid
                int effectiveUserId = (int)Interop.geteuid();
                return effectiveUserId == 0;
            }
            catch ( DllNotFoundException )
            {
                // Fallback logic when the libc library is not found
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "sudo",
                        Arguments = "-n true",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    },
                };

                try
                {
                    _ = process.Start();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
                catch
                {
                    // Failed to execute the 'sudo' command
                    return null;
                }
            }
        }

        private static class Interop
        {
            [DllImport( dllName: "libc" )]
            public static extern uint geteuid();
        }

        [NotNull]
        private static List<ProcessStartInfo> GetProcessStartInfos(
            [NotNull] FileInfo programFile,
            [CanBeNull] string cmdlineArgs
        )
        {
            if ( programFile == null )
                throw new ArgumentNullException( nameof( programFile ) );

            cmdlineArgs = cmdlineArgs ?? string.Empty;

            return new List<ProcessStartInfo>
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
                    WindowStyle = ProcessWindowStyle.Hidden,
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
                },
                // try without redirecting output
                new ProcessStartInfo
                {
                    FileName = programFile.FullName, Arguments = cmdlineArgs, UseShellExecute = false,
                },
                // try using native shell (doesn't support output redirection, perhaps they need admin)
                new ProcessStartInfo
                {
                    FileName = programFile.FullName,
                    Arguments = cmdlineArgs,
                    UseShellExecute = IsShellExecutionSupported(), // not supported on all OS's.
                },
            };
        }

        public static async Task<(int, string, string)> ExecuteProcessAsync(
            [CanBeNull] FileInfo programFile,
            string cmdlineArgs = "",
            int timeout = 0,
            bool noAdmin = false
        )
        {
            if ( programFile is null )
                throw new ArgumentNullException( nameof( programFile ) );

            if ( timeout == 0 )
                timeout = 180000; // todo: set timeout as user configurable

            List<ProcessStartInfo> processStartInfosWithFallbacks = GetProcessStartInfos( programFile, cmdlineArgs );

            Exception ex = null;
            bool? isAdmin = IsExecutorAdmin();
            for ( int index = 0; index < processStartInfosWithFallbacks.Count; index++ )
            {
                ProcessStartInfo startInfo = processStartInfosWithFallbacks[index];
                try
                {
                    using ( var cancellationTokenSource = new CancellationTokenSource( timeout ) )
                    using ( var process = new Process() )
                    {
                        if ( noAdmin && !startInfo.UseShellExecute )
                        {
                            startInfo.EnvironmentVariables["__COMPAT_LAYER"] = "RunAsInvoker";
                        }

                        process.StartInfo = startInfo;

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
                                }
                                catch ( Exception cancellationException )
                                {
                                    Logger.LogException( cancellationException );
                                }
                            }

                            _ = cancellationTokenSource.Token.Register( Callback );
                        }

                        // Start the process
                        var output = new StringBuilder();
                        var error = new StringBuilder();

                        using ( var outputWaitHandle = new AutoResetEvent( false ) )
                        using ( var errorWaitHandle = new AutoResetEvent( false ) )
                        {
                            process.OutputDataReceived += ( sender, e ) =>
                            {
                                try
                                {
                                    if ( e?.Data is null )
                                    {
                                        _ = outputWaitHandle.Set();
                                    }
                                    else
                                    {
                                        _ = output.AppendLine( e.Data );
                                        Logger.Log( e.Data );
                                    }


                                }
                                catch ( Exception exception )
                                {
                                    Logger.LogException( exception, "Exception while gathering the output from executed program" );
                                }
                            };
                            process.ErrorDataReceived += ( sender, e ) =>
                            {
                                try
                                {
                                    if ( e?.Data is null )
                                    {
                                        _ = errorWaitHandle.Set();
                                    }
                                    else
                                    {
                                        _ = error.AppendLine( e.Data );
                                        Logger.LogError( e.Data );
                                    }
                                }
                                catch ( Exception exception )
                                {
                                    Logger.LogException( exception, "Exception while gathering the error output from executed program" );
                                }
                            };

                            if ( !process.Start() )
                            {
                                throw new InvalidOperationException( "Failed to start the process." );
                            }

                            if ( process.StartInfo.RedirectStandardOutput )
                            {
                                process.BeginOutputReadLine();
                            }

                            if ( process.StartInfo.RedirectStandardError )
                            {
                                process.BeginErrorReadLine();
                            }

                            // Start the process and asynchronously wait for its completion
                            _ = await Task.Run(
                                () =>
                                {
                                    try
                                    {
                                        process.WaitForExit();
                                        return ( process.ExitCode, output.ToString(), error.ToString() );
                                    }
                                    catch ( Exception exception )
                                    {
                                        Logger.LogException( exception, "Exception while running the executed program" );
                                        return default; // todo: exit codes for timeout and unhandled etc.
                                    }
                                },
                                cancellationTokenSource.Token
                            );
                        }

                        return timeout > 0 && cancellationTokenSource.Token.IsCancellationRequested
                            ? throw new TimeoutException( "Process timed out" )
                            : ((int, string, string))( process.ExitCode, output.ToString(), error.ToString() );
                    }
                }
                catch ( Win32Exception localException )
                {
                    await Logger.LogVerboseAsync( $"Exception occurred for startInfo: '{startInfo}'" );
                    if ( !noAdmin && isAdmin == true )
                    {
                        startInfo.Verb = "runas";
                        index--;
                        continue;
                    }

                    if ( !MainConfig.DebugLogging )
                    {
                        continue;
                    }

                    await Logger.LogExceptionAsync( localException );
                    ex = localException;
                }
                catch ( Exception startinfoException )
                {
                    await Logger.LogAsync( $"An unplanned error has occurred trying to run '{programFile.Name}'" );
                    await Logger.LogExceptionAsync( startinfoException );
                    return ( -6, string.Empty, string.Empty );
                }
            }

            await Logger.LogAsync( "Process failed to start with all possible combinations of arguments." );
            await Logger.LogExceptionAsync( ex ?? new InvalidOperationException() );
            return ( -1, string.Empty, string.Empty );
        }
    }
}
