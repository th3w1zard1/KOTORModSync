// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    // there should only ever be one MainConfig instance created at any one time.
    // instance has GET and SET access.
    // Everyone else has readonly GET access.
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    public class MainConfig : INotifyPropertyChanged
    {
        public static DirectoryInfo SourcePath { get; private set; }
        public static DirectoryInfo DestinationPath { get; private set; }
        public static bool DebugLogging { get; private set; }
        public static DirectoryInfo LastOutputDirectory { get; private set; }
        public static bool AttemptFixes { get; private set; }
        public static bool DefaultInstall { get; private set; }

        public DirectoryInfo sourcePath
        {
            get
            {
                return SourcePath;
            }
            set
            {
                SourcePath = value;
                OnPropertyChanged(nameof(sourcePath));
                OnPropertyChanged(nameof(sourcePathFullName));
            }
        }

        [CanBeNull]
        public string sourcePathFullName => SourcePath?.FullName;

        public DirectoryInfo destinationPath
        {
            get
            {
                return DestinationPath;
            }
            set
            {
                DestinationPath = value;
                OnPropertyChanged(nameof(destinationPath));
                OnPropertyChanged(nameof(destinationPathFullName));
            }
        }

        [CanBeNull]
        public string destinationPathFullName => DestinationPath?.FullName;

        public bool debugLogging { get { return DebugLogging; } set { DebugLogging = value; } }
        public DirectoryInfo lastOutputDirectory { get { return LastOutputDirectory; } set { LastOutputDirectory = value; } }
        public bool defaultInstall { get { return DefaultInstall; } set { DefaultInstall = value; } }
        public bool attemptFixes { get { return AttemptFixes; } set { AttemptFixes = value; } }

        public MainConfig()
        {
            attemptFixes = false;
            debugLogging = true;
            defaultInstall = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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