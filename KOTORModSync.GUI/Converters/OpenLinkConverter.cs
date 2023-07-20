// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public class OpenLinkConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            if ( value is string url )
            {
                OpenLink( url );
            }

            return null;
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => throw new NotImplementedException();

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
                            FileName = url,
                            UseShellExecute = true
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
