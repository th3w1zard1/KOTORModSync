// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemPathing;

namespace KOTORModSync.Core.Utility
{
    public static class Utility
    {
	    [NotNull]
        public static string ReplaceCustomVariables( [NotNull] string path )
        {
            if ( path is null )
                throw new ArgumentNullException( nameof( path ) );

            return path.Replace( oldValue: "<<modDirectory>>", MainConfig.SourcePath?.FullName )
                .Replace( oldValue: "<<kotorDirectory>>", MainConfig.DestinationPath?.FullName );
        }

        [NotNull]
        public static string RestoreCustomVariables( [NotNull] string fullPath )
        {
            if ( fullPath is null )
                throw new ArgumentNullException( nameof( fullPath ) );

            return fullPath.Replace( MainConfig.SourcePath?.FullName ?? string.Empty, newValue: "<<modDirectory>>" )
                .Replace( MainConfig.DestinationPath?.FullName ?? string.Empty, newValue: "<<kotorDirectory>>" );
        }

        [NotNull]
        public static string GetExecutingAssemblyDirectory()
        {
            string executingAssemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if ( string.IsNullOrEmpty( executingAssemblyLocation ) )
            {
                executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
            }

            if ( string.IsNullOrEmpty( executingAssemblyLocation ) )
            {
                executingAssemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.GetDirectoryName( executingAssemblyLocation )
                ?? throw new InvalidOperationException( "Could not determine the path to the program!" );
        }

        [CanBeNull]
        public static object GetEnumDescription( [NotNull] Enum value )
        {
            if ( value is null )
                throw new ArgumentNullException( nameof( value ) );

            Type type = value.GetType();
            string name = Enum.GetName( type, value );
            if ( name is null )
                return null;

            FieldInfo field = type.GetField( name );

            DescriptionAttribute attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? name;
        }

        public static bool IsDirectoryWritable( [NotNull] DirectoryInfo dirPath )
        {
            if ( dirPath is null )
                throw new ArgumentNullException( nameof( dirPath ) );
            
            try
            {
                using (
                    File.Create(
                        Path.Combine(
                            PathHelper.GetCaseSensitivePath(dirPath).FullName,
                            Path.GetRandomFileName()
                        ),
                        bufferSize: 1,
                        FileOptions.DeleteOnClose
                    )
                ) { }

                return true;
            }
            catch ( UnauthorizedAccessException ex )
            {
                Logger.LogError( $"Failed to access files in the destination directory: {ex.Message}" );
            }
            catch ( PathTooLongException ex )
            {
                Logger.LogException( ex );
                Logger.LogError( $"The pathname is too long: '{dirPath.FullName}'" );
                Logger.LogError(
                    "Please utilize the registry patch that increases the Windows legacy path limit higher than 260 characters"
                    + " or move your folder/file above to a shorter directory path."
                );
            }
            catch ( IOException ex )
            {
                Logger.LogError( $"Failed to access files in the destination directory: {ex.Message}" );
            }

            return false;
        }

        [CanBeNull]
        public static DirectoryInfo ChooseDirectory()
        {
            Console.Write( "Enter the path: " );
            string thisPath = Console.ReadLine();
            if ( string.IsNullOrEmpty( thisPath ) )
            {
                return default;
            }

            thisPath = thisPath.Trim();

            if ( Directory.Exists( thisPath ) )
            {
                return new DirectoryInfo( thisPath );
            }

            Console.Write( $"Directory '{thisPath}' does not exist." );
            return default;
        }
    }
}
