﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
	// there should only ever be one MainConfig instance created at any one time.
	// instance has GET and SET access.
	// Everyone else has readonly GET access.
	[SuppressMessage(
		category: "Performance",
		checkId: "CA1822:Mark members as static",
		Justification = "unique naming scheme used for class"
	)]
	[SuppressMessage(
		category: "CodeQuality",
		checkId: "IDE0079:Remove unnecessary suppression",
		Justification = "<Pending>"
	)]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBeMadeStatic.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "InconsistentNaming")]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBePrivate.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
	public sealed class MainConfig : INotifyPropertyChanged
	{
		public enum AvailablePatchers
		{
			[Description("Use TSLPatcher")]
			TSLPatcher = 0,

			[DefaultValue(true)]
			[Description("Use HoloPatcher")]
			HoloPatcher = 1,
		}

		[Description("Only components with the selected compatibility level will be installed")]
		public enum CompatibilityLevel
		{
			[Description("Fully Compatible")] Compatible = 0,
			[Description("Mostly Compatible")] MostlyCompatible = 1,
			[Description("Not Tested")] Untested = 2,
			[Description("INCOMPATIBLE")] Incompatible = 3,
		}

		public MainConfig()
		{
			currentCompatibilityLevel = CompatibilityLevel.Compatible;
			debugLogging = false;
			patcherOption = AvailablePatchers.TSLPatcher;
			attemptFixes = true;
			noAdmin = false;
			caseInsensitivePathing = true;
		}

		[NotNull]
		public static string CurrentVersion => "0.10.32";

		[UsedImplicitly]
		[NotNull]
		public static IEnumerable<string> AllCompatibilityLevels => Enum.GetValues(typeof( CompatibilityLevel ))
			.Cast<CompatibilityLevel>().Select(compatibilityLvl => compatibilityLvl.ToString());

		[UsedImplicitly]
		[NotNull]
		public static IEnumerable<string> AllAvailablePatchers => Enum.GetValues(typeof( AvailablePatchers ))
			.Cast<AvailablePatchers>().Select(patcher => patcher.ToString());

		public static bool NoAdmin { get; private set; }

		public bool noAdmin
		{
			get => NoAdmin;
			set => NoAdmin = value && Environment.OSVersion.Platform == PlatformID.Win32NT;
		}

		public bool useCopyForMoveActions
		{
			get => UseCopyForMoveActions;
			set => UseCopyForMoveActions = value && Environment.OSVersion.Platform == PlatformID.Win32NT;
		}

		public static bool UseCopyForMoveActions { get; private set; }

		public static bool UseMultiThreadedIO { get; private set; }
		public bool useMultiThreadedIO { get => UseMultiThreadedIO; set => UseMultiThreadedIO = value; }

		public static bool CaseInsensitivePathing { get; private set; }

		public bool caseInsensitivePathing
		{
			get => CaseInsensitivePathing;
			set => CaseInsensitivePathing = value && Environment.OSVersion.Platform != PlatformID.Win32NT;
		}

		public static bool DebugLogging { get; private set; }
		public bool debugLogging { get => DebugLogging; set => DebugLogging = value; }

		public static DirectoryInfo LastOutputDirectory { get; private set; }

		[CanBeNull] public DirectoryInfo lastOutputDirectory
		{
			get => LastOutputDirectory;
			set => LastOutputDirectory = value;
		}

		public static bool AttemptFixes { get; private set; }
		public bool attemptFixes { get => AttemptFixes; set => AttemptFixes = value; }

		public static bool ArchiveDeepCheck { get; private set; }
		public bool archiveDeepCheck { get => ArchiveDeepCheck; set => ArchiveDeepCheck = value; }

		public static AvailablePatchers PatcherOption { get; private set; }

		public AvailablePatchers patcherOption
		{
			get => PatcherOption;
			set
			{
				PatcherOption = value;
				OnPropertyChanged();
			}
		}

		[NotNull] public string patcherOptionString
		{
			get
			{
				try
				{
					return PatcherOption.ToString() ?? throw new InvalidOperationException();
				}
				catch ( Exception e )
				{
					Logger.LogException(e);
				}

				return "UNDEFINED";
			}
			set => PatcherOption = (AvailablePatchers)Enum.Parse(typeof( AvailablePatchers ), value);
		}

		public static CompatibilityLevel CurrentCompatibilityLevel { get; private set; }

		public CompatibilityLevel currentCompatibilityLevel
		{
			get => CurrentCompatibilityLevel;
			set
			{
				CurrentCompatibilityLevel = value;
				OnPropertyChanged();
			}
		}

		[NotNull] public string currentCompatibilityString
		{
			get
			{
				try
				{
					return CurrentCompatibilityLevel.ToString() ?? throw new InvalidOperationException();
				}
				catch ( Exception e )
				{
					Logger.LogException(e);
				}

				return "UNDEFINED";
			}
			set => CurrentCompatibilityLevel = (CompatibilityLevel)Enum.Parse(typeof( CompatibilityLevel ), value);
		}

		[NotNull][ItemNotNull] public static List<Component> AllComponents { get; set; } = new List<Component>();

		[NotNull][ItemNotNull] public List<Component> allComponents
		{
			get => AllComponents;
			set => AllComponents = value ?? throw new ArgumentNullException(nameof( value ));
		}

		[CanBeNull] public static DirectoryInfo SourcePath { get; private set; }

		[CanBeNull] public DirectoryInfo sourcePath
		{
			get => SourcePath;
			set
			{
				SourcePath = value;
				OnPropertyChanged(nameof( sourcePathFullName ));
			}
		}

		[CanBeNull] public string sourcePathFullName => SourcePath?.FullName;

		[CanBeNull] public static DirectoryInfo DestinationPath { get; private set; }

		[CanBeNull] public DirectoryInfo destinationPath
		{
			get => DestinationPath;
			set
			{
				DestinationPath = value;
				OnPropertyChanged(nameof( destinationPathFullName ));
			}
		}

		[CanBeNull] public string destinationPathFullName => DestinationPath?.FullName;

		// used for the ui.
		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
