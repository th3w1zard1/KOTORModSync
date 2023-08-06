// Copyright 2021-2023 KOTORModSync
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
using static KOTORModSync.Core.Instruction;

namespace KOTORModSync.Core
{
    // there should only ever be one MainConfig instance created at any one time.
    // instance has GET and SET access.
    // Everyone else has readonly GET access.
    [SuppressMessage( category: "Performance", checkId: "CA1822:Mark members as static", Justification = "<Pending>" )]
    [SuppressMessage(
        category: "CodeQuality",
        checkId: "IDE0079:Remove unnecessary suppression",
        Justification = "<Pending>"
    )]
    public sealed class MainConfig : INotifyPropertyChanged
    {
        [NotNull]
        public static string CurrentVersion => "0.9.1";

        public MainConfig()
        {
            currentCompatibilityLevel = CompatibilityLevel.Compatible;
            debugLogging = false;
            patcherOption = AvailablePatchers.PyKotorCLI;
            attemptFixes = true;
            defaultInstall = true;
            noAdmin = false;
        }

        [Description( "Only components with the selected compatibility level will be installed" )]
        public enum CompatibilityLevel
        {
            [Description( "Fully Compatible" )] Compatible = 0,

            [Description( "Mostly Compatible" )] MostlyCompatible = 1,

            [Description( "Not Tested" )] Untested = 2,

            [Description( "INCOMPATIBLE" )] Incompatible = 3,
        }

        [UsedImplicitly]
        [NotNull]
        public static IEnumerable<string> AllCompatibilityLevels => Enum.GetValues(typeof(CompatibilityLevel))
            .Cast<CompatibilityLevel>()
            .Select(compatibilityLvl => compatibilityLvl.ToString());

        public enum AvailablePatchers
        {
            [Description( "Use TSLPatcher" )]
            TSLPatcher = 0,

            [DefaultValue( true )]
            [Description( "Use PyKotorCLI" )]
            PyKotorCLI = 1,
        }
        
        [UsedImplicitly]
        [NotNull]
        public static IEnumerable<string> AllAvailablePatchers => Enum.GetValues(typeof(AvailablePatchers))
            .Cast<AvailablePatchers>()
            .Select(patcher => patcher.ToString());

        public static DirectoryInfo SourcePath { get; private set; }
        public static DirectoryInfo DestinationPath { get; private set; }
        public static bool DebugLogging { get; private set; }
        public static DirectoryInfo LastOutputDirectory { get; private set; }
        public static bool AttemptFixes { get; private set; }
        public static bool DefaultInstall { get; private set; }
        public static AvailablePatchers PatcherOption { get; private set; }
        public string patcherOptionString
        {
            get => PatcherOption.ToString();
            set => PatcherOption = (AvailablePatchers)Enum.Parse( typeof( AvailablePatchers ), value );
        }
        public static CompatibilityLevel CurrentCompatibilityLevel { get; private set; }
        public string currentCompatibilityString
        {
            get => CurrentCompatibilityLevel.ToString();
            set => CurrentCompatibilityLevel = (CompatibilityLevel)Enum.Parse( typeof( CompatibilityLevel ), value );
        }
        [NotNull][ItemNotNull] public static List<Component> AllComponents { get; set; } = new List<Component>();

        [NotNull]
        public List<Component> allComponents
        {
            get => AllComponents;
            set => AllComponents = value ?? throw new ArgumentNullException( nameof( value ) );
        }

        [CanBeNull]
        public DirectoryInfo sourcePath
        {
            get => SourcePath;
            set
            {
                SourcePath = value;
                OnPropertyChanged( nameof( sourcePathFullName ) );
            }
        }

        [CanBeNull] public string sourcePathFullName => SourcePath?.FullName;

        [CanBeNull]
        public DirectoryInfo destinationPath
        {
            get => DestinationPath;
            set
            {
                DestinationPath = value;
                OnPropertyChanged( nameof( destinationPathFullName ) );
            }
        }

        [CanBeNull] public string destinationPathFullName => DestinationPath?.FullName;

        public AvailablePatchers patcherOption
        {
            get => PatcherOption;
            set
            {
                PatcherOption = value;
                OnPropertyChanged();
            }
        }

        public CompatibilityLevel currentCompatibilityLevel
        {
            get => CurrentCompatibilityLevel;
            set
            {
                CurrentCompatibilityLevel = value;
                OnPropertyChanged();
            }
        }

        public bool debugLogging { get => DebugLogging; set => DebugLogging = value; }

        [CanBeNull]
        public DirectoryInfo lastOutputDirectory { get => LastOutputDirectory; set => LastOutputDirectory = value; }

        public bool defaultInstall { get => DefaultInstall; set => DefaultInstall = value; }

        public bool attemptFixes { get => AttemptFixes; set => AttemptFixes = value; }

        public static bool NoAdmin { get; private set; }

        public bool noAdmin { get => NoAdmin; set => NoAdmin = value; }

        public event PropertyChangedEventHandler PropertyChanged;

        // used for the ui.
        private void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }
}
