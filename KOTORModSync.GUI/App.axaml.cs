// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public class App : Application
	{
		public override void Initialize() => AvaloniaXamlLoader.Load( this );

		public override void OnFrameworkInitializationCompleted()
		{
			if ( ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop )
			{
				try
				{
					// Subscribe to the UnobservedTaskException event
					TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

					desktop.MainWindow = new MainWindow();
					Logger.Log( "Started main window" );
				}
				catch ( Exception ex )
				{
					Logger.LogException( ex );
				}
			}

			base.OnFrameworkInitializationCompleted();
		}

		// ReSharper disable once MemberCanBeMadeStatic.Local
		private void HandleUnobservedTaskException( [CanBeNull] object sender, UnobservedTaskExceptionEventArgs e )
		{
			// Log or handle the unobserved task exception here
			Logger.LogException( e.Exception );
			e.SetObserved(); // Mark the exception as observed to prevent it from crashing the application
		}
	}
}
