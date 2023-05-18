// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace KOTORModSync.Core
{
    public class MainConfig
    {
        public static DirectoryInfo SourcePath { get; private set; }
        public static DirectoryInfo DestinationPath { get; private set; }
        public static List<Component> Components { get; }

        public static DirectoryInfo LastOutputDirectory;
        public static DirectoryInfo ModConfigPath;

        public void UpdateConfig(DirectoryInfo sourcePath, DirectoryInfo destinationPath)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
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
