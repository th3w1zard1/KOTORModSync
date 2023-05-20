// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NUnit.Framework;
using SharpCompress.Readers;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class CommandExecutorTests
    {
        public void ExecuteCommand(string command, ManualResetEvent completed, Dictionary<string, object> sharedData)
        {
            try
            {
                Logger.Log("Testing TryExecuteCommand...");
                var result = PlatformAgnosticMethods.TryExecuteCommand(command);
                sharedData["success"] = result.Success;
                sharedData["output"] = result.Output;
            }
            catch (Exception ex) when (ex is TimeoutException)
            {
                Logger.Log("The test timed out. Make sure the command execution is completing within the expected time.");
                Logger.Log("Here are the currently running processes on the machine:");
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    Logger.Log($"{process.ProcessName} (ID: {process.Id})");
                }

                Logger.Log("Standard output from the timed-out process:");
                var standardOutput = PlatformAgnosticMethods.TryExecuteCommand("echo");
                Logger.Log(standardOutput.Output);
            }
            finally
            {
                completed.Set();
            }
        }

        [Test]
        [Timeout(10000)]
        public void TryExecuteCommand_ShouldReturnSuccessAndOutput_OnWindows()
        {
            // Arrange
            const string command = "echo Hello, Windows!";
            const string expectedOutput = "Hello, Windows!";
            ConfigureEnvironmentForWindows();

            // Act
            var completed = new ManualResetEvent(false);
            var sharedData = new Dictionary<string, object>();

            // Start a separate thread to execute the command
            var thread = new Thread(() => ExecuteCommand(command, completed, sharedData));
            thread.Start();

            // Wait for completion or timeout
            if (!completed.WaitOne(11000)) // Wait for completion with a 1-second buffer
            {
                Logger.Log("The test did not complete within the expected time.");
                Logger.Log("The test thread is still running.");

                // Interrupt the thread to signal cancellation
                thread.Interrupt();

                // Wait for the thread to complete
                thread.Join();

                // You can add additional actions or assertions here as needed
            }
            else if (thread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                // The test thread is still running
                Logger.Log("The test thread is still running.");
            }

            // Assert
            bool success = (bool)sharedData["success"];
            string output = (string)sharedData["output"];
            Assert.IsTrue(success);
            Assert.That(output, Is.EqualTo(expectedOutput));
        }

        [Test]
        [Timeout(10000)]
        public void TryExecuteCommand_ShouldReturnSuccessAndOutput_OnMac()
        {
            // Arrange
            const string command = "echo Hello, Mac!";
            const string expectedOutput = "Hello, Mac!";
            ConfigureEnvironmentForMac();

            // Act
            var completed = new ManualResetEvent(false);
            var sharedData = new Dictionary<string, object>();

            // Start a separate thread to execute the command
            var thread = new Thread(() => ExecuteCommand(command, completed, sharedData));
            thread.Start();

            // Wait for completion or timeout
            if (!completed.WaitOne(11000)) // Wait for completion with a 1-second buffer
            {
                Logger.Log("The test did not complete within the expected time.");
                Logger.Log("The test thread is still running.");

                // Interrupt the thread to signal cancellation
                thread.Interrupt();

                // Wait for the thread to complete
                thread.Join();

                // You can add additional actions or assertions here as needed
            }
            else if (thread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                // The test thread is still running
                Logger.Log("The test thread is still running.");
            }

            // Assert
            bool success = (bool)sharedData["success"];
            string output = (string)sharedData["output"];
            Assert.IsTrue(success);
            Assert.That(output, Is.EqualTo(expectedOutput));
        }

        [Test]
        [Timeout(10000)]
        public void TryExecuteCommand_ShouldReturnSuccessAndOutput_OnLinux()
        {
            // Arrange
            const string command = "echo Hello, Linux!";
            const string expectedOutput = "Hello, Linux!";
            ConfigureEnvironmentForLinux();

            // Act
            var completed = new ManualResetEvent(false);
            var sharedData = new Dictionary<string, object>();

            // Start a separate thread to execute the command
            var thread = new Thread(() => ExecuteCommand(command, completed, sharedData));
            thread.Start();

            // Wait for completion or timeout
            if (!completed.WaitOne(11000)) // Wait for completion with a 1-second buffer
            {
                Logger.Log("The test did not complete within the expected time.");
                Logger.Log("The test thread is still running.");

                // Interrupt the thread to signal cancellation
                thread.Interrupt();

                // Wait for the thread to complete
                thread.Join();

                // You can add additional actions or assertions here as needed
            }
            else if (thread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                // The test thread is still running
                Logger.Log("The test thread is still running.");
            }

            // Assert
            bool success = (bool)sharedData["success"];
            string output = (string)sharedData["output"];
            Assert.IsTrue(success);
            Assert.That(output, Is.EqualTo(expectedOutput));
        }

        [Test]
        [Timeout(10000)]
        public void TryExecuteCommand_ShouldReturnFailure_OnUnsupportedPlatform()
        {
            // Arrange
            const string command = "echo Hello, Unsupported!";
            ConfigureEnvironmentForUnsupportedPlatform();

            // Act
            var completed = new ManualResetEvent(false);
            var sharedData = new Dictionary<string, object>();

            // Start a separate thread to execute the command
            var thread = new Thread(() => ExecuteCommand(command, completed, sharedData));
            thread.Start();

            // Wait for completion or timeout
            if (!completed.WaitOne(11000)) // Wait for completion with a 1-second buffer
            {
                Logger.Log("The test did not complete within the expected time.");
                Logger.Log("The test thread is still running.");

                // Interrupt the thread to signal cancellation
                thread.Interrupt();

                // Wait for the thread to complete
                thread.Join();

                // You can add additional actions or assertions here as needed
            }
            else if (thread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                // The test thread is still running
                Logger.Log("The test thread is still running.");
            }

            // Assert
            bool success = (bool)sharedData["success"];
            string output = (string)sharedData["output"];
            Assert.IsFalse(success);
            Assert.That(output, Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetAvailableMemory_ShouldReturnNonZero_OnSupportedPlatform()
        {
            // Act
            long availableMemory = PlatformAgnosticMethods.GetAvailableMemory();

            // Assert
            Assert.That(availableMemory, Is.GreaterThan(0));
        }

        [Test]
        public void GetAvailableMemory_ShouldReturnDefaultValue_OnUnsupportedPlatform()
        {
            // Arrange
            ConfigureEnvironmentForUnsupportedPlatform();

            // Act
            long availableMemory = PlatformAgnosticMethods.GetAvailableMemory();

            // Assert
            Assert.That(availableMemory, Is.EqualTo(4L * 1024 * 1024 * 1024)); // 4GB
        }

        private static void ConfigureEnvironmentForWindows()
        {
            // Configure environment for Windows
            Environment.SetEnvironmentVariable("OS", "Windows_NT");
            Environment.SetEnvironmentVariable("ComSpec", "cmd.exe");
        }

        private static void ConfigureEnvironmentForMac()
        {
            // Configure environment for Mac
            Environment.SetEnvironmentVariable("OS", "Darwin");
            Environment.SetEnvironmentVariable("SHELL", "sh");
        }

        private static void ConfigureEnvironmentForLinux()
        {
            // Configure environment for Linux
            Environment.SetEnvironmentVariable("OS", "Linux");
            Environment.SetEnvironmentVariable("SHELL", "/bin/sh");
        }

        private static void ConfigureEnvironmentForUnsupportedPlatform()
        {
            // Configure environment for an unsupported platform
            Environment.SetEnvironmentVariable("OS", "");
            Environment.SetEnvironmentVariable("SHELL", "");
        }

    }
}
