// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class Utility
    {
        public static string ReplaceCustomVariables
            ( string path ) => path.Replace( "<<modDirectory>>", MainConfig.SourcePath.FullName )
            .Replace( "<<kotorDirectory>>", MainConfig.DestinationPath.FullName );

        public static string RestoreCustomVariables
            ( string fullPath ) => fullPath.Replace( MainConfig.SourcePath.FullName, "<<modDirectory>>" )
            .Replace( MainConfig.DestinationPath.FullName, "<<kotorDirectory>>" );

        public static bool IsDirectoryWritable( [CanBeNull] DirectoryInfo dirPath )
        {
            if ( dirPath is null ) throw new ArgumentNullException( nameof( dirPath ) );

            try
            {
                using ( FileStream fs = File.Create(
                           Path.Combine( dirPath.FullName, Path.GetRandomFileName() ),
                           1,
                           FileOptions.DeleteOnClose
                       ) )
                {
                }

                return true;
            }
            catch ( UnauthorizedAccessException ex )
            {
                Logger.Log( $"Failed to access files in the destination directory: {ex.Message}" );
            }
            catch ( PathTooLongException ex )
            {
                Logger.LogException( ex );
                Logger.Log( $"The pathname is too long: '{dirPath.FullName}'" );
                Logger.Log(
                    "Please utilize the registry patch that increases the Windows legacy path limit of 260 characters or move your KOTOR2 installation to a shorter directory path."
                );
            }
            catch ( IOException ex )
            {
                Logger.Log( $"Failed to access files in the destination directory: {ex.Message}" );
            }

            return false;
        }

        public static async Task WaitForProcessExitAsync( [NotNull] Process process, string processName )
        {
            // Wait for the process to exit
            while ( !process.HasExited )
                await Task.Delay( 1000 ); // 1 second is the recommended default

            // Make sure the process exited correctly
            if ( process.ExitCode != 0 )
            {
                throw new Exception( $"The process {processName} exited with code {process.ExitCode}." );
            }
        }

        [CanBeNull]
        public static DirectoryInfo ChooseDirectory()
        {
            Console.Write( "Enter the path: " );
            string thisPath = Console.ReadLine()?.Trim();

            if ( Directory.Exists( thisPath ) )
            {
                return new DirectoryInfo( thisPath );
            }

            Console.Write( $"Directory '{thisPath}' does not exist." );
            return default;
        }
    }
}
