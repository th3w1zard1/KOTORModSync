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
			if ( fileSystemInfos is null )
				throw new ApplicationException("Path is invalid?");

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
			OSPlatform thisOperatingSystem = Utility.Utility.GetOperatingSystem();
			if ( thisOperatingSystem == OSPlatform.Windows )
			{
				await Logger.LogAsync($"Step 1: Attempt to take ownership of the target '{pathInfo.FullName}'...");
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
					await Logger.LogWarningAsync(
						$"Failed to take ownership of '{pathInfo.FullName}':{Environment.NewLine}"
						+ $"exit code: {takeOwnershipResult.Item1}{Environment.NewLine}"
						+ $"stdout: {takeOwnershipResult.Item2}{Environment.NewLine}"
						+ $"stderr: {takeOwnershipResult.Item3}"
					);
				}


				await Logger.LogAsync($"Step 2: Attempting to set access rights of the target '{pathInfo.FullName}' using icacls...");
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
					await Logger.LogWarningAsync(
						$"Could not set Windows icacls permissions at '{pathInfo.FullName}':{Environment.NewLine}"
						+ $"exit code: {icaclsResult.Item1}{Environment.NewLine}"
						+ $"stdout: {icaclsResult.Item2}{Environment.NewLine}"
						+ $"stderr: {icaclsResult.Item3}"
					);
				}

				await Logger.LogAsync("Step 3: Acquiring permissions with .net code...");
				await Logger.LogVerboseAsync(takeOwnershipResult.Item2);
				await Logger.LogVerboseAsync(icaclsResult.Item2);
			}
			else
			{
				(int, string, string) chmodResult = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "chmod",
					$"-R 777 {pathInfo.FullName}",
					askAdmin: true,
					useShellExecute: false,
					hideProcess: !MainConfig.DebugLogging
				);
				(int, string, string) chownResult = await PlatformAgnosticMethods.ExecuteProcessAsync(
					programFile: "chown",
					$"-R $(whoami) {pathInfo.FullName}",
					askAdmin: true,
					useShellExecute: false,
					hideProcess: !MainConfig.DebugLogging
				);

				if ( chmodResult.Item1 != 0 || chownResult.Item1 != 0 )
				{
					if ( chmodResult.Item1 != 0 )
					{
						await Logger.LogWarningAsync(
							$"Could not set unix chmod permissions at '{pathInfo}':{Environment.NewLine}"
							+ $"exit code: {chmodResult.Item1}{Environment.NewLine}"
							+ $"stdout: {chmodResult.Item2}{Environment.NewLine}"
							+ $"stderr: {chmodResult.Item3}"
						);
					}

					if ( chownResult.Item1 != 0 )
					{
						await Logger.LogWarningAsync(
							$"Could not set unix chown permissions at '{pathInfo}':{Environment.NewLine}"
							+ $"exit code: {chownResult.Item1}{Environment.NewLine}"
							+ $"stdout: {chownResult.Item2}{Environment.NewLine}"
							+ $"stderr: {chownResult.Item3}"
						);
					}
				}

				await Logger.LogVerboseAsync(chmodResult.Item2);
				await Logger.LogVerboseAsync(chownResult.Item2);
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
