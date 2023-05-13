using System.Collections.Generic;
using System.IO;

namespace KOTORModSync.Core
{
    public class MainConfig
    {
        private static DirectoryInfo _sourcePath;
        private static DirectoryInfo _destinationPath;
        private static List<Component> _components;

        public static DirectoryInfo SourcePath => _sourcePath;
        public static DirectoryInfo DestinationPath => _destinationPath;
        public static List<Component> Components => _components;

        public static DirectoryInfo LastOutputDirectory;
        public static DirectoryInfo ModConfigPath;

        public void UpdateConfig(DirectoryInfo sourcePath, DirectoryInfo destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
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
