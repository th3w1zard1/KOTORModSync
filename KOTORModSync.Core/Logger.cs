// Copyright 2021-2023 KOTORModSync
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
        private static async Task LogInternalAsync( [CanBeNull] string internalMessage, bool fileOnly = false )
        {
            internalMessage = internalMessage ?? string.Empty;

            await s_semaphore.WaitAsync();
            try
            {
                string logMessage = $"[{DateTime.Now}] {internalMessage}";
                if ( !fileOnly )
                {
                    await Console.Out.WriteLineAsync( logMessage );
                }

                // Debug.WriteLine( logMessage );

                string formattedDate = DateTime.Now.ToString( "yyyy-MM-dd" );
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var token = cancellationTokenSource.Token;

                bool fileWritten = false;

                while (!fileWritten)
                {
                    try
                    {
                        // Attempt to write to the file
                        using (var writer = new StreamWriter(LogFileName + formattedDate + ".txt", append: true))
                        {
                            await writer.WriteLineAsync(logMessage + Environment.NewLine);
                            fileWritten = true; // Successfully written, exit the loop
                        }
                    }
                    catch (IOException)
                    {
                        // File is locked; wait a bit before retrying
                        await Task.Delay(100, token); // Wait 100 milliseconds
                    }

                    // Throw if cancellation has been requested
                    token.ThrowIfCancellationRequested();
                }

                Logged.Invoke( logMessage ); // Raise the Logged event
            }
            catch ( Exception ex )
            {
                Console.WriteLine( ex );
            }
            finally
            {
                _ = s_semaphore.Release();
            }
        }
        
        public static void Log( [CanBeNull] string message, bool fileOnly = false ) => Task.Run( async () => await LogInternalAsync( message, fileOnly ) );

        [NotNull]
        public static Task LogAsync( [CanBeNull] string message ) => LogInternalAsync( message );

        public static void LogVerbose( [CanBeNull] string message ) =>
            Log( $"[Verbose] {message}", !MainConfig.DebugLogging );

        [NotNull]
        public static Task LogVerboseAsync( [CanBeNull] string message ) =>
            LogInternalAsync( $"[Verbose] {message}", !MainConfig.DebugLogging );

        // ReSharper disable once UnusedMember.Global
        public static void LogWarning( [NotNull] string message ) => Log( $"[Warning] {message}" );

        [NotNull]
        public static Task LogWarningAsync( [NotNull] string message ) =>
            LogInternalAsync( $"[Warning] {message}" );

        public static void LogError( [CanBeNull] string message ) => Log( $"[Error] {message}" );

        [NotNull]
        public static Task LogErrorAsync( [CanBeNull] string message ) =>
            LogInternalAsync( $"[Error] {message}" );

        [NotNull]
        public static async Task LogExceptionAsync( [CanBeNull] Exception ex ) =>
            await Task.Run( () => LogException( ex ) );

        [NotNull]
        public static async Task LogExceptionAsync( [CanBeNull] Exception ex, [CanBeNull] string customMessage ) =>
            await Task.Run( () => LogException( ex, customMessage ) );

        public static void LogException( [CanBeNull] Exception exception, [CanBeNull] string customMessage )
        {
            exception = exception ?? new ApplicationException();

            LogException( exception );
            LogError( customMessage );
        }

        public static void LogException( [CanBeNull] Exception ex )
        {
            ex = ex ?? new ApplicationException();

            Log( $"Exception: {ex.GetType()?.Name} - {ex.Message}" );
            Log( $"Stack trace: {ex.StackTrace}" );

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
