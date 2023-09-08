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
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemPathing
{
	// This class will provide easy usages of a case insensitive path.
	// By default (_realTimeUpdating = false), we only resolve case sensitivity on construction
	// When _realTimeUpdating is true, calling Exists or FullName etc will always perform those checks case-insensitively.
	[SuppressMessage( category: "ReSharper", checkId: "UnusedMember.Global" )]
	public sealed class InsensitivePath
	{
		private readonly bool _realTimeUpdating;
		private readonly object _lockObject = new object();
		[CanBeNull] private FileSystemInfo _fileSystemInfo { get; set; }
		private readonly string OriginalPath;
		public bool? IsFile { get; }

		public InsensitivePath(FileSystemInfo fileSystemInfo, bool realTimeUpdating = false)
		{
			_realTimeUpdating = realTimeUpdating;
			OriginalPath = fileSystemInfo.FullName;
			IsFile = fileSystemInfo is FileInfo;
			_fileSystemInfo = fileSystemInfo;
			
			Refresh();
		}

		public InsensitivePath(string inputPath, bool? isFile = null, bool realTimeUpdating = false)
		{
			_realTimeUpdating = realTimeUpdating;
			string formattedPath = PathHelper.FixPathFormatting(inputPath);
			OriginalPath = formattedPath;
			IsFile = isFile ?? DetermineIsFile(formattedPath);
			if ( isFile == true )
				_fileSystemInfo = (FileSystemInfo)new FileInfo(formattedPath);
			else if ( isFile == false )
				_fileSystemInfo = new DirectoryInfo(formattedPath);
			Refresh();
		}


		public bool Exists => _fileSystemInfo?.Exists ?? ExistsRightNow();

		private bool ExistsRightNow()
		{
			switch ( IsFile )
			{
				case true:
					return File.Exists(_fileSystemInfo.FullName);
				case false:
					return Directory.Exists(_fileSystemInfo.FullName);
				default:
					return File.Exists(_fileSystemInfo.FullName) || Directory.Exists(_fileSystemInfo.FullName);
			}
		}

		public string FullName
		{
			get
			{
				_ = Exists;
				return _fileSystemInfo.FullName;
			}
		}

		public string Name
		{
			get
			{
				_ = Exists;
				return _fileSystemInfo.Name;
			}
		}

		public void Delete()
		{
			_ = Exists;
			_fileSystemInfo.Delete();
			GetDuplicates()?.ToList().ForEach(duplicate => duplicate?.Delete());
		}

		public void Refresh()
		{
			_fileSystemInfo.Refresh();

			// don't resolve case sensitivity if platform is windows or if the fileSystemInfo already exists.
			if ( _fileSystemInfo.Exists || RuntimeInformation.IsOSPlatform(OSPlatform.Windows) )
				return;

			lock ( _lockObject )
			{
				if ( IsFile == null && ExistsRightNow() )
					return;

				(string fileSystemItemPath, bool? isFile) = PathHelper.GetCaseSensitivePath(OriginalPath, IsFile);

				switch ( isFile )
				{
					case true:
						_fileSystemInfo = new FileInfo(fileSystemItemPath);
						break;
					case false:
						_fileSystemInfo = new DirectoryInfo(fileSystemItemPath);
						break;
				}
			}
		}
		
		private static bool? DetermineIsFile(string path)
		{
			if (File.Exists(path))
				return true;
			if (Directory.Exists(path))
				return false;
			return null; // Neither file nor directory exists
		}

		public List<FileSystemInfo> GetDuplicates() =>
			PathHelper.FindCaseInsensitiveDuplicates(_fileSystemInfo.FullName, includeSubFolders: false, IsFile)
				.ToList();
		
		private Type GetTargetType()
		{
			switch ( IsFile )
			{
				case true:
					return typeof( FileInfo );
				case false:
					return typeof( DirectoryInfo );
				default:
					return typeof( FileSystemInfo );
			}
		}

	    public static InsensitivePath operator /(string a, InsensitivePath b)
	    {
	        string pathPart = PathHelper.FixPathFormatting(a);
	        return CombineWithoutDuplicateCommonParts(pathPart, b.FullName);
	    }

	    public static InsensitivePath operator /(FileSystemInfo a, InsensitivePath b)
	    {
	        string pathPart = a.FullName;
	        bool isFile = a is FileInfo;
	        return CombineWithoutDuplicateCommonParts(pathPart, b.FullName, isFile);
	    }

	    public static InsensitivePath operator /(InsensitivePath a, string b)
	    {
	        string pathPart = PathHelper.FixPathFormatting(b);
	        return CombineWithoutDuplicateCommonParts(a.FullName, pathPart);
	    }

	    public static InsensitivePath operator /(InsensitivePath a, FileSystemInfo b)
	    {
	        string pathPart = b.FullName;
	        bool isFile = b is FileInfo;
	        return CombineWithoutDuplicateCommonParts(a.FullName, pathPart, isFile);
	    }

	    private static InsensitivePath CombineWithoutDuplicateCommonParts(string pathA, string pathB, bool? isFile = null)
	    {
	        string[] componentsA = pathA.Split(Path.DirectorySeparatorChar);
	        string[] componentsB = pathB.Split(Path.DirectorySeparatorChar);

	        int overlapStartIndex = -1;

	        for (int i = 0; i < Math.Min(componentsA.Length, componentsB.Length); i++)
	        {
	            if (componentsA[componentsA.Length - 1 - i] != componentsB[i])
	                break;

	            overlapStartIndex = i;
	        }

	        if (overlapStartIndex == -1)
	            return new InsensitivePath(Path.Combine(pathA, pathB), isFile);

	        string nonOverlappingA = string.Join(Path.DirectorySeparatorChar.ToString(), componentsA, startIndex: 0, componentsA.Length - overlapStartIndex);
	        string nonOverlappingB = string.Join(Path.DirectorySeparatorChar.ToString(), componentsB, overlapStartIndex, componentsB.Length - overlapStartIndex);

	        return new InsensitivePath(Path.Combine(nonOverlappingA, nonOverlappingB), isFile);
	    }

	    public InsensitivePath JoinPath(params object[] args)
	    {
	        InsensitivePath newPath = this;
	        foreach (object arg in args)
	        {
		        switch ( arg )
		        {
			        case string strPath:
				        newPath /= strPath;
				        break;
			        case FileSystemInfo fsInfo:
				        newPath /= fsInfo;
				        break;
		        }
	        }
	        return newPath;
	    }

		public override string ToString() => FullName;
		
		public override int GetHashCode() => OriginalPath.ToLowerInvariant().GetHashCode();
		public static bool operator ==(InsensitivePath left, object right) => left?.Equals(right) ?? right is null;
		public static bool operator !=(InsensitivePath left, object right) => !(left == right);
		public override bool Equals(object obj)
		{
			switch ( obj )
			{
				case string pathStr:
					return string.Equals(OriginalPath, pathStr, StringComparison.OrdinalIgnoreCase);
				case InsensitivePath insensPath:
					return string.Equals(
						OriginalPath,
						insensPath._fileSystemInfo.FullName,
						StringComparison.OrdinalIgnoreCase
					);
				case FileSystemInfo fileInfo:
					return string.Equals(
						OriginalPath,
						fileInfo.FullName,
						StringComparison.OrdinalIgnoreCase
					);
				default:
					return false;
			}
		}

		public static implicit operator string(InsensitivePath insensitivePath) => insensitivePath.FullName;
		public static implicit operator InsensitivePath(string pathString) => new InsensitivePath(pathString);

		public static implicit operator FileInfo(InsensitivePath insensitivePath) => insensitivePath._fileSystemInfo as FileInfo;
		public static implicit operator DirectoryInfo(InsensitivePath insensitivePath) => insensitivePath._fileSystemInfo as DirectoryInfo;
	}
}
