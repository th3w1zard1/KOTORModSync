﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    public static class Logger
    {
        private static bool s_isInitialized;
        private static readonly object s_initializationLock = new object();
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim( 1 );

        public const string LogFileName = "kotormodsync_";
        public static event Action<string> Logged = delegate { };
        public static event Action<Exception> ExceptionLogged = delegate { };

        public static void Initialize()
        {
            if ( s_isInitialized )
            {
                return;
            }

            lock ( s_initializationLock )
            {
                s_isInitialized = true;

                Log( $"Logging initialized at {DateTime.Now}" );

                // Set up unhandled exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
        }


        [NotNull]
        private static async Task LogInternalAsync(
            [CanBeNull] string internalMessage,
            bool fileOnly = false,
            ConsoleColor? color = null
        )
        {
            internalMessage = internalMessage ?? string.Empty;

            await s_semaphore.WaitAsync();
            try
            {
                string logMessage = $"[{DateTime.Now}] {internalMessage}";
                
                if ( !fileOnly )
                {
                    string consoleMessage = $"[{DateTime.Now:HH:mm:ss}] {internalMessage}";
                    // Set color if specified.
                    if (color.HasValue)
                        Console.ForegroundColor = color.Value;

                    await Console.Out.WriteLineAsync(consoleMessage);

                    // Reset the color before continuing.
                    if (color.HasValue)
                        Console.ResetColor();
                }

                string formattedDate = DateTime.Now.ToString("yyyy-MM-dd");
                CancellationToken token = new CancellationTokenSource(delay: TimeSpan.FromMinutes(2)).Token;

                bool fileWritten = false;
                while (!fileWritten)
                {
                    try
                    {
                        using (var writer = new StreamWriter(LogFileName + formattedDate + ".txt", append: true))
                        {
                            await writer.WriteLineAsync(logMessage + Environment.NewLine);
                            fileWritten = true;
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"IOException occurred while writing log message: {ex.Message}");
                        await Task.Delay(millisecondsDelay: 100, token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected exception occurred while writing log message: {ex}");
                        break;
                    }

                    token.ThrowIfCancellationRequested();
                }

                Logged.Invoke(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred in LogInternalAsync: {ex}");
            }
            finally
            {
                _ = s_semaphore.Release();
            }
        }

        
        public static void Log( [CanBeNull] string message, bool fileOnly = false ) => _ = LogInternalAsync( message, fileOnly );

        [NotNull]
        public static Task LogAsync( [CanBeNull] string message ) => LogInternalAsync( message );

        public static void LogVerbose( [CanBeNull] string message ) =>
            _ = LogInternalAsync( $"[Verbose] {message}", !MainConfig.DebugLogging, color: ConsoleColor.DarkGray );

        [NotNull]
        public static Task LogVerboseAsync( [CanBeNull] string message ) =>
            LogInternalAsync( $"[Verbose] {message}", !MainConfig.DebugLogging, color: ConsoleColor.DarkGray );

        // ReSharper disable once UnusedMember.Global
        public static void LogWarning( [NotNull] string message ) => _ = LogInternalAsync( $"[Warning] {message}", color: ConsoleColor.Yellow );

        [NotNull]
        public static Task LogWarningAsync( [NotNull] string message ) =>
            LogInternalAsync( $"[Warning] {message}", color: ConsoleColor.Yellow );

        public static void LogError( [CanBeNull] string message ) => _ = LogInternalAsync($"[Error] {message}", color: ConsoleColor.Red);

        [NotNull]
        public static Task LogErrorAsync( [CanBeNull] string message ) =>
            LogInternalAsync( $"[Error] {message}", color: ConsoleColor.Red);
        
        public static void LogException( [CanBeNull] Exception ex, [CanBeNull] string customMessage = null ) => _ = LogExceptionAsync( ex, customMessage );

        [NotNull]
        public static async Task LogExceptionAsync( [CanBeNull] Exception ex, [CanBeNull] string customMessage = null )
        {
            ex = ex ?? new ApplicationException();
            
            await LogErrorAsync( customMessage );
            await LogInternalAsync( $"Exception: {ex.GetType()?.Name} - {ex.Message}", color: ConsoleColor.Red );
            await LogInternalAsync( $"Stack trace:{Environment.NewLine}{ex.StackTrace}", color: ConsoleColor.Magenta );

            ExceptionLogged.Invoke( ex ); // Raise the ExceptionLogged event
        }

        private static void CurrentDomain_UnhandledException(
            [NotNull] object sender,
            [NotNull] UnhandledExceptionEventArgs e
        )
        {
            if ( !( e.ExceptionObject is Exception ex ) )
            {
                LogError( "current appdomain's unhandled exception did not have a valid exception handle?" );
                return;
            }

            LogException( ex );
        }

        private static void TaskScheduler_UnobservedTaskException(
            [NotNull] object sender,
            [NotNull] UnobservedTaskExceptionEventArgs e
        )
        {
            if ( e.Exception is null )
            {
                LogError( "appdomain's unhandledexception did not have a valid exception handle?" );
                return;
            }

            foreach ( Exception ex in e.Exception.InnerExceptions )
            {
                LogException( ex );
            }

            e.SetObserved();
        }
    }
}
