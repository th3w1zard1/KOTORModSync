// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
{
    public class InsensitivePath : FileSystemInfo
    {
        private FileSystemInfo _fileSystemInfo { get; set; }
        public List<FileSystemInfo> Duplicates { get; private set; }
        public bool? IsDirectory =>
            _fileSystemInfo is DirectoryInfo
                ? Duplicates.OfType<FileInfo>().Any()
                    ? (bool?)null
                    : true
                : Duplicates.OfType<FileInfo>().Any()
                    ? (bool?)null
                    : false;
        
        public bool? IsFile =>
            _fileSystemInfo is FileInfo
                ? Duplicates.OfType<DirectoryInfo>().Any()
                    ? (bool?)null
                    : true
                : Duplicates.OfType<DirectoryInfo>().Any()
                    ? (bool?)null
                    : false;
        
        public override string Name => _fileSystemInfo?.Name;
        public override string FullName => Exists ? _fileSystemInfo?.FullName : Resolve().FullName;
        public override bool Exists
        {
            get
            {
                if (_fileSystemInfo != null)
                    return _fileSystemInfo.Exists;

                Resolve();
                return _fileSystemInfo?.Exists ?? false;
            }
        }


        public override void Delete()
        {
            if ( !Exists ) _ = Resolve();
            _fileSystemInfo.Delete();
        }
        public override string ToString() => FullName;

        public InsensitivePath( string inputPath )
        {
            if ( string.IsNullOrEmpty( inputPath ) )
                throw new ArgumentException( "Input path cannot be null or empty.", nameof( inputPath ) );

            OriginalPath = PathHelper.FixPathFormatting( inputPath );
            _ = Resolve();
        }

        private FileSystemInfo Resolve()
        {
            if ( _fileSystemInfo?.Exists == true ) return _fileSystemInfo;
            ( FileSystemInfo fileSystemInfo, List<FileSystemInfo> duplicates ) =
                PathHelper.GetClosestMatchingEntry( OriginalPath );

            _fileSystemInfo = fileSystemInfo;
            Duplicates = duplicates;

            return fileSystemInfo;
        }

        public IEnumerable<FileSystemInfo> FindDuplicates() => PathHelper.FindCaseInsensitiveDuplicates( _fileSystemInfo.FullName );

        public static implicit operator string( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo.FullName;
        public static implicit operator FileInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as FileInfo;
        public static implicit operator DirectoryInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as DirectoryInfo;
    }
}
