using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.FileSystemUtils
{
	public static class FilePermissionHelper
	{
		public static async Task FixPermissionsAsync(FileSystemInfo pathInfo) =>
			await FixPermissionsCoreAsync(pathInfo);

		public static async Task FixPermissionsAsync(string path)
		{
			Tuple<FileInfo, DirectoryInfo> fileSystemInfos = PathHelper.TryGetValidFileSystemInfos(path);
			OSPlatform thisOS = Utility.Utility.GetOperatingSystem();
			if ( !(fileSystemInfos.Item1 is null) )
			{
				await FixPermissionsCoreAsync(fileSystemInfos.Item1);
			}

			if ( !(fileSystemInfos.Item2 is null) )
			{
				await FixPermissionsCoreAsync(fileSystemInfos.Item2);
			}
		}

		private static async Task FixPermissionsCoreAsync(FileSystemInfo pathInfo)
		{
			OSPlatform thisOS = Utility.Utility.GetOperatingSystem();
			if ( thisOS == OSPlatform.Windows )
			{
				Logger.Log($"Step 1: Attempt to take ownership of the target '{pathInfo.FullName}'...");
				(int, string, string) takeOwnershipResult = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "takeown",
					$"/F \"{pathInfo.FullName}\" /R /SKIPSL /D Y",
					askAdmin: true,
					useShellExecute: true,
					hideProcess: !MainConfig.DebugLogging,
					noLogging: true
				);

				if ( takeOwnershipResult.Item1 != 0 )
				{
					Logger.LogWarning(
						$"Failed to take ownership of '{pathInfo.FullName}':{Environment.NewLine}"
						+ $"exit code: {takeOwnershipResult.Item1}{Environment.NewLine}"
						+ $"stdout: {takeOwnershipResult.Item2}{Environment.NewLine}"
						+ $"stderr: {takeOwnershipResult.Item3}"
					);
				}


				Logger.Log($"Step 2: Attempting to set access rights of the target '{pathInfo.FullName}' using icacls...");
				// /T - apply the permissions recursively to all subdirectories and files
				// /C - continue processing even when an error occurs
				// /L - process symbolic links (and junctions) instead of the target they point to.
				// | find /V \"processed file: \"" - filters out the files/folders that do not have errors.
				(int, string, string) icaclsResult = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "icacls",
					args: $"\"{pathInfo.FullName}\" /grant *S-1-1-0:(OI)(CI)F /T /C /L",
					askAdmin: true,
					useShellExecute: true,
					hideProcess: !MainConfig.DebugLogging,
					noLogging: true
				);

				if ( icaclsResult.Item1 != 0 )
				{
					Logger.LogWarning(
						$"Could not set Windows icacls permissions at '{pathInfo.FullName}':{Environment.NewLine}"
						+ $"exit code: {icaclsResult.Item1}{Environment.NewLine}"
						+ $"stdout: {icaclsResult.Item2}{Environment.NewLine}"
						+ $"stderr: {icaclsResult.Item3}"
					);
				}

				Logger.Log("Step 3: Acquiring permissions with .net code...");
				Logger.LogVerbose(takeOwnershipResult.Item2);
				Logger.LogVerbose(icaclsResult.Item2);
			}
			else
			{
				(int, string, string) chmod_result = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "chmod",
					$"-R 777 {pathInfo.FullName}",
					askAdmin: true,
					useShellExecute: false,
					hideProcess: !MainConfig.DebugLogging
				);
				(int, string, string) chown_result = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "chown",
					$"-R $(whoami) {pathInfo.FullName}",
					askAdmin: true,
					useShellExecute: false,
					hideProcess: !MainConfig.DebugLogging
				);

				if ( chmod_result.Item1 != 0 || chown_result.Item1 != 0 )
				{
					if ( chmod_result.Item1 != 0 )
					{
						Logger.LogWarning(
							$"Could not set unix chmod permissions at '{pathInfo}':{Environment.NewLine}"
							+ $"exit code: {chmod_result.Item1}{Environment.NewLine}"
							+ $"stdout: {chmod_result.Item2}{Environment.NewLine}"
							+ $"stderr: {chmod_result.Item3}"
						);
					}

					if ( chown_result.Item1 != 0 )
					{
						Logger.LogWarning(
							$"Could not set unix chown permissions at '{pathInfo}':{Environment.NewLine}"
							+ $"exit code: {chown_result.Item1}{Environment.NewLine}"
							+ $"stdout: {chown_result.Item2}{Environment.NewLine}"
							+ $"stderr: {chown_result.Item3}"
						);
					}
				}

				Logger.LogVerbose(chmod_result.Item2);
				Logger.LogVerbose(chown_result.Item2);
			}
			SetDotNetPermissions(pathInfo);
		}

		private static void SetDotNetPermissions(FileSystemInfo pathInfo)
		{
			// Fallback to .NET API
			try
			{
				if ( pathInfo is FileInfo filePathInfo )
				{
					filePathInfo.Attributes &= ~FileAttributes.ReadOnly & ~FileAttributes.System;
				}
				else if ( pathInfo is DirectoryInfo dirPathInfo )
				{
					dirPathInfo.Attributes &= ~FileAttributes.ReadOnly & ~FileAttributes.System;
					foreach ( FileInfo subFilePathInfo in dirPathInfo.EnumerateFilesSafely() )
					{
						subFilePathInfo.Attributes &= ~FileAttributes.ReadOnly & ~FileAttributes.System;
					}

					foreach ( DirectoryInfo subDirPathInfo in dirPathInfo.EnumerateDirectoriesSafely(
							searchOption: SearchOption.AllDirectories
						) )
					{
						subDirPathInfo.Attributes &= ~FileAttributes.ReadOnly & ~FileAttributes.System;
					}
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(
					e,
					customMessage:
					$"Failed to set file/folder permissions of {pathInfo.FullName} with dotnet code, does the file/folder really exist?"
				);
			}
		}
	}
}
