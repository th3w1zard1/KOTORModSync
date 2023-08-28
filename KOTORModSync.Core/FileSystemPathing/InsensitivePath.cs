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
	// This class will provide easy usages of a case insensitive path.
	// Calling Exists or FullName etc will always perform those checks case-insensitively.
	[SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
	public sealed class InsensitivePath : DynamicObject
    {
	    public InsensitivePath( FileSystemInfo fileSystemInfo )
        {
	        OriginalPath = fileSystemInfo.FullName;
	        _isFile = fileSystemInfo is FileInfo;
	        _fileSystemInfo = fileSystemInfo;
        }

	    public InsensitivePath( string inputPath, bool isFile )
        {
            string formattedPath = PathHelper.FixPathFormatting( inputPath );
            _isFile = isFile;
            _fileSystemInfo = _isFile
                ? new FileInfo( formattedPath )
                : (FileSystemInfo)new DirectoryInfo( formattedPath );
            OriginalPath = formattedPath;

            Refresh();
        }

	    [NotNull] private FileSystemInfo _fileSystemInfo { get; set; }
	    private bool _isFile { get; }
	    public bool IsFile => _isFile;

	    public bool Exists
        {
            get
            {
                if ( IsFile && File.Exists( _fileSystemInfo.FullName ) )
                    return true;
                if ( !IsFile && Directory.Exists( _fileSystemInfo.FullName ) )
                    return true;

                if ( MainConfig.CaseInsensitivePathing )
                    Refresh();

                return _fileSystemInfo.Exists;
            }
        }

	    public string FullName
        {
	        get
	        {
		        if ( Exists || !MainConfig.CaseInsensitivePathing)
					return _fileSystemInfo.FullName;
				
		        if ( MainConfig.CaseInsensitivePathing )
			        Refresh();
		        return _fileSystemInfo.FullName;
	        }
        }

	    public string Name
        {
	        get
	        {
		        if ( Exists || !MainConfig.CaseInsensitivePathing)
			        return _fileSystemInfo.Name;
				
		        if ( MainConfig.CaseInsensitivePathing )
			        Refresh();
		        return _fileSystemInfo.Name;
	        }
        }

	    private string OriginalPath { get; }

	    public List<FileSystemInfo> FindDuplicates()
        {
			return PathHelper.FindCaseInsensitiveDuplicates(
		        _fileSystemInfo.FullName,
		        includeSubFolders: true,
		        isFile: IsFile
	        ).ToList();
        }

	    public void Delete()
        {
            _fileSystemInfo.Delete();
            FindDuplicates()?.ToList().ForEach(duplicate => duplicate?.Delete());
        }

	    public override string ToString() => _fileSystemInfo.FullName;

	    public void Refresh()
        {
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
        }

	    public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
	        MemberInfo memberInfo = _fileSystemInfo.GetType().GetMember(binder.Name).FirstOrDefault();
			
	        switch ( memberInfo )
	        {
		        case PropertyInfo propertyInfo:
			        result = propertyInfo.GetValue(_fileSystemInfo);
			        return true;
		        case MethodInfo methodInfo:
			        object Invoker( object[] args ) => methodInfo.Invoke( _fileSystemInfo, args );

			        result = (Func<object[], object>)Invoker;
			        return true;
		        case FieldInfo fieldInfo:
			        result = fieldInfo.GetValue(_fileSystemInfo);
			        return true;
				default:
					result = null;
					return false;
	        }
        }

	    public override bool Equals(object obj)
        {
	        switch ( obj )
	        {
		        case string pathStr:
			        return string.Equals(_fileSystemInfo.FullName, pathStr, StringComparison.OrdinalIgnoreCase);
		        case FileSystemInfo fileInfo:
			        return string.Equals(_fileSystemInfo.FullName, fileInfo.FullName, StringComparison.OrdinalIgnoreCase);
		        case InsensitivePath insensPath:
			        return string.Equals(_fileSystemInfo.FullName, insensPath._fileSystemInfo.FullName, StringComparison.OrdinalIgnoreCase);
		        default:
			        return false;
	        }
        }

	    // ReSharper disable once NonReadonlyMemberInGetHashCode - we only reference the string, not the object, so this warning can be ignored.
	    public override int GetHashCode() => _fileSystemInfo.FullName.ToLowerInvariant().GetHashCode();

	    public static bool operator ==(InsensitivePath left, object right) => left?.Equals( right ) ?? right is null;
	    public static bool operator !=(InsensitivePath left, object right) => !(left == right);


	    public static implicit operator string( InsensitivePath insensitivePath ) => insensitivePath.FullName;
	    public static implicit operator FileInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as FileInfo;
	    public static implicit operator DirectoryInfo( InsensitivePath insensitivePath ) => insensitivePath._fileSystemInfo as DirectoryInfo;
    }
}
