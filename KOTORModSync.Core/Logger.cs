// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace KOTORModSync.Core
{
    public static class Logger
    {
        public static event Action<string> Logged = delegate { };
        public static event Action<Exception> ExceptionLogged = delegate { };

        private const string LogFileName = "log.txt";
        private static bool s_isInitialized = false;
        private static readonly object s_initializationLock = new object();

        public static void Initialize()
        {
            if (s_isInitialized)
                return;

            lock (s_initializationLock)
            {
                if (s_isInitialized)
                    return;

                s_isInitialized = true;

                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);

                Log($"Logging initialized at {DateTime.Now}");

                // Set up unhandled exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
        }

        public static void Log(string message)
        {
            string logMessage = $"[{DateTime.Now}] {message}";
            Console.WriteLine(logMessage);
            Debug.WriteLine(logMessage);
            File.AppendAllText(LogFileName, logMessage + Environment.NewLine);

            Logged.Invoke(logMessage); // Raise the Logged event
        }

        public static void LogVerbose(string message)
        {
            if (MainConfig.DebugLogging)
            {
                Log(message);
            }
        }

        public static void LogException(Exception ex)
        {
            Log($"Exception: {ex.GetType().Name} - {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");

            ExceptionLogged.Invoke(ex); // Raise the ExceptionLogged event
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex);
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                foreach (Exception ex in e.Exception.InnerExceptions)
                {
                    LogException(ex);
                }
            }

            e.SetObserved();
        }
    }
}
