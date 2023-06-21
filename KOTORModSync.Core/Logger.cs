// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core
{
    public static class Logger
    {
        private const string LogFileName = "kotormodsync_";

        private static bool s_isInitialized;
        private static readonly object s_initializationLock = new object();
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim( 1 );
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
                if ( s_isInitialized )
                {
                    return;
                }

                s_isInitialized = true;

                Log( $"Logging initialized at {DateTime.Now}" );

                // Set up unhandled exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
        }

        public static void Log( string message, bool fileOnly = false )
        {
            string logMessage = $"[{DateTime.Now}] {message}";
            if ( !fileOnly )
            {
                Console.WriteLine( logMessage );
            }

            Debug.WriteLine( logMessage );

            string formattedDate = DateTime.Now.ToString( "yyyy-MM-dd" );
            File.AppendAllText( LogFileName + formattedDate + ".txt", logMessage + Environment.NewLine );

            Logged.Invoke( logMessage ); // Raise the Logged event
        }

        private static async Task LogInternalAsync( string internalMessage, bool fileOnly = false )
        {
            await s_semaphore.WaitAsync();
            try
            {
                string logMessage = $"[{DateTime.Now}] {internalMessage}";
                if ( !fileOnly )
                {
                    await Console.Out.WriteLineAsync( logMessage );
                }

                Debug.WriteLine( logMessage );

                string formattedDate = DateTime.Now.ToString( "yyyy-MM-dd" );
                using ( var writer = new StreamWriter( LogFileName + formattedDate + ".txt", true ) )
                {
                    await writer.WriteLineAsync( logMessage + Environment.NewLine );
                }

                Logged.Invoke( logMessage ); // Raise the Logged event
            }
            finally
            {
                _ = s_semaphore.Release();
            }
        }

        public static Task LogAsync( string message ) => LogInternalAsync( message );
        public static void LogVerbose( string message ) => Log( "[Verbose] " + message, fileOnly:true );
        public static Task LogVerboseAsync( string message ) => LogInternalAsync( "[Verbose] " + message, fileOnly: true );
        public static void LogWarning( string message ) => Log( "[Warning] " + message );
        public static Task LogWarningAsync( string message ) => LogInternalAsync( "[Warning] " + message );
        public static void LogError( string message ) => Log( "[Error] " + message );
        public static Task LogErrorAsync( string message ) => LogInternalAsync( "[Error} " + message );
        public static async Task LogExceptionAsync( Exception ex ) => await Task.Run( () => LogException( ex ) );
        public static async Task LogExceptionAsync( Exception ex, string customMessage ) => await Task.Run( () => LogException( ex, customMessage ) );

        public static void LogException( Exception exception, string customMessage )
        {
            LogException( exception );
            LogError( customMessage );
        }

        public static void LogException( Exception ex )
        {
            Log( $"Exception: {ex.GetType().Name} - {ex.Message}" );
            Log( $"Stack trace: {ex.StackTrace}" );

            if ( !MainConfig.DebugLogging )
            {
                return;
            }

            StackTrace stackTrace = new StackTrace( ex, true );
            for ( int i = 0; i < stackTrace.FrameCount; i++ )
            {
                StackFrame frame = stackTrace.GetFrame( i );
                MethodBase method = frame.GetMethod();
                Type declaringType = method.DeclaringType;

                Log( $"Frame {i}:" );
                Log( $"Method: {declaringType?.FullName}.{method.Name}" );
                Log( $"File: {frame.GetFileName()}" );
                Log( $"Line: {frame.GetFileLineNumber()}" );
            }

            ExceptionLogged.Invoke( ex ); // Raise the ExceptionLogged event
        }

        private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
        {
            if ( !( e.ExceptionObject is Exception ex ) )
            {
                return;
            }

            LogException( ex );
        }

        private static void TaskScheduler_UnobservedTaskException( object sender, UnobservedTaskExceptionEventArgs e )
        {
            if ( e.Exception == null )
            {
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
