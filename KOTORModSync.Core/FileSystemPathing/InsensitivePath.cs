// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
{
    [SuppressMessage(category: "ReSharper", checkId: "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
    public class InsensitivePath : FileSystemInfo
    {
        [CanBeNull] private FileSystemInfo _fileSystemInfo { get; set; }
        private bool? _isFile { get; set; }
        public bool? IsFile => _isFile ??
            _fileSystemInfo is FileInfo
                ? true
                : _fileSystemInfo is DirectoryInfo
                    ? false
                    : (bool?)null;
        public List<FileSystemInfo> Duplicates { get; private set; } = new List<FileSystemInfo>();
        public List<FileSystemInfo> FindDuplicates() => PathHelper.FindCaseInsensitiveDuplicates( FullName, includeSubFolders: true, isFile: IsFile ).ToList();
        public override string Name => _fileSystemInfo?.Name ?? Path.GetFileName( OriginalPath );
        public override string FullName => _fileSystemInfo?.FullName ?? OriginalPath;
        public override bool Exists => _fileSystemInfo?.Exists ?? false;
        public static InsensitivePath Empty { get; } = new InsensitivePath();
        private InsensitivePath() { }
        public override void Delete()
        {
            if ( Directory.Exists( Path.GetDirectoryName(FullName) ) )
                _fileSystemInfo?.Delete();
        }
        public override string ToString() => FullName;

        public InsensitivePath( string inputPath, bool? isFile = null )
        {
            if ( string.IsNullOrWhiteSpace( inputPath ) )
                throw new ArgumentException( "Input path cannot be null or empty or whitespace.", nameof( inputPath ) );

            string formattedPath = PathHelper.FixPathFormatting( inputPath );
            OriginalPath = formattedPath;
            _isFile = isFile;
            Refresh();
        }
        
        public new void Refresh()
        {
            if ( IsFile == true && File.Exists( _fileSystemInfo?.FullName ) )
            {
                // ReSharper disable once PossibleNullReferenceException
                // can't be null otherwise File.Exists would fail?
                _fileSystemInfo.Refresh();
                return;
            }

            if ( IsFile == false && Directory.Exists( _fileSystemInfo?.FullName ) )
            {
                _fileSystemInfo.Refresh();
                return;
            }

            (string fileSystemItemPath, bool? isFile) = PathHelper.GetCaseSensitivePath( OriginalPath );

            if ( isFile == true )
                _fileSystemInfo = new FileInfo( fileSystemItemPath );
            else if ( isFile == false )
                _fileSystemInfo = new DirectoryInfo( fileSystemItemPath );
        }

        public override bool Equals(object obj) => obj is InsensitivePath other && Equals( other );
        public bool Equals(InsensitivePath other)
        {
            return other is null
                ? _fileSystemInfo is null
                : ReferenceEquals( this, other ) || ( _fileSystemInfo is null && other._fileSystemInfo is null );
        }

        // ReSharper disable once NonReadonlyMemberInGetHashCode - TODO:
        public override int GetHashCode() => _fileSystemInfo?.GetHashCode() ?? 0;
        
        public static bool operator !=(InsensitivePath left, InsensitivePath right) => !(left == right);
        public static bool operator ==(InsensitivePath left, InsensitivePath right) =>
            ReferenceEquals( left, right )
            || (
                !( left is null )
                && !( right is null )
                && left.Equals( right )
            );


        //public static implicit operator string( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo?.FullName;
        public static implicit operator FileInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as FileInfo;
        public static implicit operator DirectoryInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as DirectoryInfo;
    }
}
