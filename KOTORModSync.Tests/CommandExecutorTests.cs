// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class CommandExecutorTests
    {
        private static void ExecuteCommand(
            string command,
            EventWaitHandle completed,
            IDictionary<string, object> sharedData
        )
        {
            try
            {
                Logger.Log( "Testing TryExecuteCommand..." );
                (int exitCode, string output, string error) = PlatformAgnosticMethods.TryExecuteCommand( command );
                sharedData["success"] = exitCode == 0;
                sharedData["output"] = output;
                sharedData["error"] = error;
            }
            catch ( Exception ex ) when ( ex is TimeoutException )
            {
                Logger.Log(
                    "The test timed out. Make sure the command execution is completing within the expected time."
                );
                Logger.Log( "Here are the currently running processes on the machine:" );
                Process[] processes = Process.GetProcesses();
                foreach ( Process process in processes )
                {
                    Logger.Log( $"'{process.ProcessName}' (ID: {process.Id})" );
                }

                Logger.Log( "Standard output from the timed-out process:" );
                (int exitCode, string output, string error) = PlatformAgnosticMethods.TryExecuteCommand( "echo" );
                Logger.Log( output );
            }
            finally
            {
                _ = completed.Set();
            }
        }

        [Test]
        [Timeout( 10000 )]
        public void TryExecuteCommand_ShouldReturnSuccessAndOutput()
        {
            // Arrange
            const string command = "echo Hello, Windows!";
            const string expectedOutput = "Hello, Windows!";

            // Act
            var completed = new ManualResetEvent( false );
            var sharedData = new Dictionary<string, object>();

            // Start a separate thread to execute the command
            var thread = new Thread( () => ExecuteCommand( command, completed, sharedData ) );
            thread.Start();

            // Wait for completion or timeout
            if ( !completed.WaitOne( 11000 ) ) // Wait for completion with a 1-second buffer
            {
                Logger.Log( "The test did not complete within the expected time." );
                Logger.Log( "The test thread is still running." );

                // Interrupt the thread to signal cancellation
                thread.Interrupt();

                // Wait for the thread to complete
                thread.Join();

                // You can add additional actions or assertions here as needed
            }
            else if ( thread.ThreadState != System.Threading.ThreadState.Stopped )
            {
                // The test thread is still running
                Logger.Log( "The test thread is still running." );
            }

            // Assert
            bool success = (bool)sharedData["success"];
            string output = (string)sharedData["output"];
            Assert.Multiple(
                () =>
                {
                    Assert.That( success );
                    Assert.That( output.Trim(), Is.EqualTo( expectedOutput ) );
                }
            );
        }

        [Test]
        public void GetAvailableMemory_ShouldReturnNonZero_OnSupportedPlatform()
        {
            // Act
            long availableMemory = PlatformAgnosticMethods.GetAvailableMemory();

            // Assert
            Assert.That( availableMemory, Is.GreaterThan( 0 ) );
        }
    }
}
