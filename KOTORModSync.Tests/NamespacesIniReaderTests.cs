// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using KOTORModSync.Core.TSLPatcher;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class NamespacesIniReaderTests
    {
        private static Stream CreateNamespacesIniStream(string content)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(byteArray);
        }

        [Test]
        [Ignore("needs update")]
        public void ReadNamespacesIniFromArchive_WhenValidInput_ReturnsNamespaces()
        {
            // Arrange
            const string content = @"
[Namespaces]
Namespace1=standard
Namespace2=hk50
Namespace3=standardTSLRCM
Namespace4=hk50TSLRCM
";
            Stream stream = CreateNamespacesIniStream(content);

            // Act
            Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive(stream);

            // Assert
            Assert.IsNotNull(result);
            Assert.That( result.Count, Is.EqualTo( 4 ) );
            Assert.That( result["Namespace1"], Is.EqualTo( "standard" ) );
            Assert.That( result["Namespace2"], Is.EqualTo( "hk50" ) );
            Assert.That( result["Namespace3"], Is.EqualTo( "standardTSLRCM" ) );
            Assert.That( result["Namespace4"], Is.EqualTo( "hk50TSLRCM" ) );
        }

        [Test]
        public void ReadNamespacesIniFromArchive_WhenTslPatchDataFolderNotFound_ReturnsNull()
        {
            // Arrange
            const string content = @"
[Namespaces]
Namespace1=standard
Namespace2=hk50
Namespace3=standardTSLRCM
Namespace4=hk50TSLRCM
";
            Stream stream = CreateNamespacesIniStream(content);

            // Act
            Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive(stream);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ReadNamespacesIniFromArchive_WhenInvalidContent_ReturnsNull()
        {
            // Arrange
            const string content = "Invalid Content";
            Stream stream = CreateNamespacesIniStream(content);

            // Act
            Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive(stream);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        [Ignore("needs update")]
        public void ParseNamespacesIni_WhenValidInput_ReturnsNamespaces()
        {
            // Arrange
            const string content = @"
Namespace1=standard
Namespace2=hk50
Namespace3=standardTSLRCM
Namespace4=hk50TSLRCM
";
            using (var reader = new StreamReader(CreateNamespacesIniStream(content)))
            {
                // Act
                Dictionary<string, Dictionary<string, string>> result = IniHelper.ParseNamespacesIni(reader);

                // Assert
                Assert.IsNotNull(result);
                Assert.That( result.Count, Is.EqualTo( 4 ) );
                Assert.That( result["Namespace1"], Is.EqualTo( "standard" ) );
                Assert.That( result["Namespace2"], Is.EqualTo( "hk50" ) );
                Assert.That( result["Namespace3"], Is.EqualTo( "standardTSLRCM" ) );
                Assert.That( result["Namespace4"], Is.EqualTo( "hk50TSLRCM" ) );
            }
        }

        [Test]
        public void ParseNamespacesIni_WhenInvalidInput_ThrowsArgumentNullException()
        {
            // Arrange
            StreamReader? reader = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => IniHelper.ParseNamespacesIni(reader));
        }

        [Test]
        public void ParseNamespacesIni_WhenInvalidContent_ReturnsEmptyDictionary()
        {
            // Arrange
            string content = "Invalid Content";
            using (var reader = new StreamReader(CreateNamespacesIniStream(content)))
            {
                // Act
                Dictionary<string, Dictionary<string, string>> result = IniHelper.ParseNamespacesIni(reader);

                // Assert
                Assert.IsNotNull(result);
                Assert.That( result.Count, Is.EqualTo( 0 ) );
            }
        }
    }
}
