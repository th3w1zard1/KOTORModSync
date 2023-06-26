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

namespace KOTORModSync.Core
{
    // there should only ever be one MainConfig instance created at any one time.
    // instance has GET and SET access.
    // Everyone else has readonly GET access.
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    [SuppressMessage( "Performance", "CA1822:Mark members as static", Justification = "<Pending>" )]
    [SuppressMessage( "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>" )]
    public class MainConfig : INotifyPropertyChanged
    {
        public MainConfig()
        {
            this.currentCompatibilityLevel = CompatibilityLevel.Compatible;
            this.debugLogging = true;
            this.patcherOption = AvailablePatchers.TSLPatcher;
            this.attemptFixes = true;
            this.defaultInstall = true;
            this.noAdmin = false;
        }

        [Description( "Only components with the selected compatibility level will be installed" )]
        public enum CompatibilityLevel
        {
            [Description( "Fully Compatibile" )]
            Compatible = 0,

            [Description( "Mostly Compatible" )]
            MostlyCompatible = 1,

            [Description( "Not Tested" )]
            Untested = 2,

            [Description( "INCOMPATIBLE" )]
            Incompatible = 3
        }

        public IEnumerable<CompatibilityLevel> AllCompatibilityLevels
        {
            get { return Enum.GetValues( typeof( CompatibilityLevel ) ).Cast<CompatibilityLevel>(); }
        }


        public enum AvailablePatchers
        {
            [DefaultValue( true )]
            [Description( "Use TSLPatcher" )]
            TSLPatcher = 0,

            [Category( "Not Tested - use as own risk" )]
            [Description( "Use TSLPatcherCLI" )]
            TSLPatcherCLI = 1, // not tested

            //[Description( "Use HoloPatcher" )]
            //HoloPatcher = 2
        }

        public IEnumerable<AvailablePatchers> AllAvailablePatchers
        {
            get { return Enum.GetValues( typeof( AvailablePatchers ) ).Cast<AvailablePatchers>(); }
        }


        public static DirectoryInfo SourcePath { get; private set; }
        public static DirectoryInfo DestinationPath { get; private set; }
        public static bool DebugLogging { get; private set; }
        public static DirectoryInfo LastOutputDirectory { get; private set; }
        public static bool AttemptFixes { get; private set; }
        public static bool DefaultInstall { get; private set; }
        public static AvailablePatchers PatcherOption { get; private set; }
        public static CompatibilityLevel CurrentCompatibilityLevel { get; private set; }

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
            set => PatcherOption = value;
        }

        public CompatibilityLevel currentCompatibilityLevel
        {
            get => CurrentCompatibilityLevel;
            set => CurrentCompatibilityLevel = value;
        }

        public bool debugLogging
        {
            get => DebugLogging;
            set => DebugLogging = value;
        }

        public DirectoryInfo lastOutputDirectory
        {
            get => LastOutputDirectory;
            set => LastOutputDirectory = value;
        }

        public bool defaultInstall
        {
            get => DefaultInstall;
            set => DefaultInstall = value;
        }

        public bool attemptFixes
        {
            get => AttemptFixes;
            set => AttemptFixes = value;
        }

        public static bool NoAdmin { get; private set; }

        public bool noAdmin
        {
            get => NoAdmin;
            set => NoAdmin = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;


        // used for the ui.
        protected virtual void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null )
        {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }

    public static class ModDirectory
    {
        public class ArchiveEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public class ZipTree
        {
            public string Filename { get; set; }
            public string Name { get; set; }
            public bool IsFile { get; set; }
            public List<ZipTree> Children { get; set; } = new List<ZipTree>();
        }
    }
}
