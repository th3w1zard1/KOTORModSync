// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

/* Unmerged change from project 'KOTORModSync (net462)'
Before:
// The .NET Foundation licenses this file to you under the MIT license.
After:
// The .txt file in the you under the MIT license.
*/
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public class OpenLinkConverter : IValueConverter
	{
		public object Convert(
			[CanBeNull] object value,
			[NotNull] Type targetType,
			[CanBeNull] object parameter,
			[NotNull] CultureInfo culture
		)
		{
			if ( value is string url )
			{
				OpenLink( url );
			}

			return null;
		}

		public object ConvertBack(
			[CanBeNull] object value,
			[NotNull] Type targetType,
			[CanBeNull] object parameter,
			[NotNull] CultureInfo culture
		) => throw new NotImplementedException();

		private static void OpenLink( [NotNull] string url )
		{
			try
			{
				if ( url is null )
					throw new ArgumentNullException( nameof( url ) );

				if ( !Uri.TryCreate( url, UriKind.Absolute, out Uri _ ) )
					throw new ArgumentException( "Invalid URL" );

				if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
				{
					_ = Process.Start(
						new ProcessStartInfo
						{
							FileName = url, UseShellExecute = true,
						}
					);
				}
				else if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
				{
					_ = Process.Start( fileName: "open", url );
				}
				else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) )
				{
					_ = Process.Start( fileName: "xdg-open", url );
				}
				else
				{
					Logger.LogError( "Unsupported platform, cannot open link." );
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException( ex, $"Failed to open URL: {ex.Message}" );
			}
		}
	}
}
