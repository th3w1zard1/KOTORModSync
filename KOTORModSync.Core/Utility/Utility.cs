// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
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

        [CanBeNull]
        public static object GetEnumDescription( Enum value )
        {
            Type type = value.GetType();
            string name = Enum.GetName( type, value );
            if ( name == null )
                return null;

            FieldInfo field = type.GetField( name );
            if ( field == null )
                return null;

            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description;
        }


        public static bool IsDirectoryWritable( [CanBeNull] DirectoryInfo dirPath )
        {
            if ( dirPath is null ) throw new ArgumentNullException( nameof( dirPath ) );

            try
            {
                using (
                    File.Create(
                        Path.Combine( dirPath.FullName, Path.GetRandomFileName() ),
                        1,
                        FileOptions.DeleteOnClose
                    )
                )
                { }

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
                    "Please utilize the registry patch that increases the Windows legacy path limit higher than 260 characters"
                    + " or move your folder/file above to a shorter directory path."
                );
            }
            catch ( IOException ex )
            {
                Logger.Log( $"Failed to access files in the destination directory: {ex.Message}" );
            }

            return false;
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
