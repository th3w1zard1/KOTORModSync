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
        public static event Action<string> Logged = delegate { };
        public static event Action<Exception> ExceptionLogged = delegate { };

        private static bool s_isInitialized = false;
        private static readonly object s_initializationLock = new object();
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim( 1 );

        private const string LogFileName = "kotormodsync_";

        public static void Initialize()
        {
            if ( s_isInitialized )
                return;

            lock ( s_initializationLock )
            {
                if ( s_isInitialized )
                    return;

                s_isInitialized = true;

                Log( $"Logging initialized at {DateTime.Now}" );

                // Set up unhandled exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
        }

        public static Task LogAsync( string message )
        {
            return LogInternalAsync( message ); // Start the logging operation asynchronously without awaiting it
        }

        private static async Task LogInternalAsync( string internalMessage )
        {
            await s_semaphore.WaitAsync();
            try
            {
                string logMessage = $"[{DateTime.Now}] {internalMessage}";
                await Console.Out.WriteLineAsync( logMessage );
                Debug.WriteLine( logMessage );

                string formattedDate = DateTime.Now.ToString( "yyyy-MM-dd" );
                using ( StreamWriter writer = new StreamWriter( LogFileName + formattedDate + ".txt", true ) )
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

        public static void Log( string message )
        {
            string logMessage = $"[{DateTime.Now}] {message}";
            Console.WriteLine( logMessage );
            Debug.WriteLine( logMessage );

            string formattedDate = DateTime.Now.ToString( "yyyy-MM-dd" );
            File.AppendAllText( LogFileName + formattedDate + ".txt", logMessage + Environment.NewLine );

            Logged.Invoke( logMessage ); // Raise the Logged event
        }

        public static void LogVerbose( string message )
        {
            if ( !MainConfig.DebugLogging )
                return;

            Log( "[Verbose] " + message );
        }

        public static async Task LogVerboseAsync( string message )
        {
            if ( !MainConfig.DebugLogging )
                return;

            await LogAsync( "[Verbose] " + message );
        }

        public static void LogWarning( string message ) => Log( "[Warning] " + message );
        public static void LogError( string message ) => Log( "[Error] " + message );

        public static Task LogWarningAsync( string message ) => LogAsync( "[Warning] " + message );
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
                return;

            // Get the current stack frame
            var frame = new StackFrame( 1, true );
            MethodBase method = frame.GetMethod();
            Type declaringType = method.DeclaringType;

            // Get the locals for the current stack frame
            LocalVariableInfo[] localVariables = method.GetMethodBody()?.LocalVariables?.ToArray();

            // Log local variables information
            if ( localVariables == null )
                return;

            Log( "Local Variables:" );

            foreach ( LocalVariableInfo local in localVariables )
            {
                string localName = method.GetMethodBody()?.LocalVariables[local.LocalIndex].ToString();

                if ( localName == null )
                    continue;

                if ( declaringType == null )
                    continue;

                MemberInfo[] members = declaringType.GetMember(
                    localName,
                    BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static );

                if ( members.Length == 0 )
                    continue;

                object value = null;
                MemberInfo member = members[0];
                switch ( member )
                {
                    case FieldInfo field when field.IsPublic || field.IsAssembly || field.IsFamilyOrAssembly:
                        value = field.GetValue( member.ReflectedType );
                        break;
                    case PropertyInfo property when ( property.CanRead
                        && ( property.GetGetMethod( true )?.IsPublic ?? false ) ):
                        {
                            value = property.GetValue( null );
                            break;
                        }
                }

                Log( $"- {localName}: {value}" );
            }

            ExceptionLogged.Invoke( ex ); // Raise the ExceptionLogged event
        }

        private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
        {
            if ( !( e.ExceptionObject is Exception ex ) )
                return;

            LogException( ex );
        }

        private static void TaskScheduler_UnobservedTaskException( object sender, UnobservedTaskExceptionEventArgs e )
        {
            if ( e.Exception == null )
                return;

            foreach ( Exception ex in e.Exception.InnerExceptions )
                LogException( ex );

            e.SetObserved();
        }
    }
}
