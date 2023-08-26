// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
{
    [SuppressMessage(category: "ReSharper", checkId: "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
    public class InsensitivePath : FileSystemInfo
    {
        [NotNull] private FileSystemInfo _fileSystemInfo { get; set; }
        private bool _isFile { get; }
        public bool IsFile => _isFile;
        public List<FileSystemInfo> FindDuplicates() => PathHelper.FindCaseInsensitiveDuplicates( FullName, includeSubFolders: true, isFile: IsFile ).ToList();
        public override string Name => _fileSystemInfo.Name;
        public override string FullName => _fileSystemInfo.FullName;
        public override bool Exists
        {
            get
            {
                if ( IsFile && File.Exists( FullName ) )
                    return true;
                if ( !IsFile && Directory.Exists( FullName ) )
                    return true;

                if ( MainConfig.CaseInsensitivePathing )
                    Refresh();

                return _fileSystemInfo.Exists;
            }
        }
        public override void Delete()
        {
            _fileSystemInfo.Delete();
            FindDuplicates()?.ToList().ForEach(duplicate => duplicate?.Delete());
        }

        public override string ToString() => FullName;
        
        public InsensitivePath( FileSystemInfo fileSystemInfo ) => _fileSystemInfo = fileSystemInfo;
        public InsensitivePath( string inputPath, bool isFile )
        {
            string formattedPath = PathHelper.FixPathFormatting( inputPath );
            OriginalPath = formattedPath;
            _isFile = isFile;
            _fileSystemInfo = _isFile
                ? (FileSystemInfo)new FileInfo( formattedPath )
                : (FileSystemInfo)new DirectoryInfo( formattedPath );

            Refresh();
        }

        public new void Refresh()
        {
            if ( _fileSystemInfo is null )
                throw new NullReferenceException("_fileSystemInfo cannot be null");

            _fileSystemInfo.Refresh();

            if ( _fileSystemInfo.Exists )
                return;
            if ( !MainConfig.CaseInsensitivePathing )
                return;

            ( string fileSystemItemPath, bool? isFile ) = PathHelper.GetCaseSensitivePath( OriginalPath );

            switch ( isFile )
            {
                case true:
                    _fileSystemInfo = new FileInfo(fileSystemItemPath);
                    break;
                case false:
                    _fileSystemInfo = new DirectoryInfo(fileSystemItemPath);
                    break;
                default:
                    return;
            }

            _fileSystemInfo.Refresh();
        }

        //public static implicit operator string( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo?.FullName;
        public static implicit operator FileInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as FileInfo;
        public static implicit operator DirectoryInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as DirectoryInfo;
    }
}
