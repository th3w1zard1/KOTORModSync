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
        public void ReadNamespacesIniFromArchive_WhenValidInput_ReturnsNamespaces()
        {
            // Arrange
            const string content = @"
[Namespaces]
Namespace1=standard
Namespace2=hk50
Namespace3=standardTSLRCM
Namespace4=hk50TSLRCM

[standard]
Name=standard hk47 no tslrcm

[hk50]
Name=hk50 no tslrcm

[standardTSLRCM]
Name=standard hk47 with tslrcm

[hk50TSLRCM]
Name=hk50 with tslrcm
";
            Stream stream = CreateNamespacesIniStream(content);

            // Act
            Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive(stream);

            // Assert
            Assert.That( result, Is.Not.Null );
            Assert.That( result, Has.Count.EqualTo( 4 ) );

            Assert.Multiple( () =>
            {
                Assert.That( result["Namespace1"]["standard"], Is.EqualTo( "standard hk47 no tslrcm" ) );
                Assert.That( result["Namespace2"]["hk50"], Is.EqualTo( "hk50 no tslrcm" ) );
                Assert.That( result["Namespace3"]["standardTSLRCM"], Is.EqualTo( "standard hk47 with tslrcm" ) );
                Assert.That( result["Namespace4"]["hk50TSLRCM"], Is.EqualTo( "hk50 with tslrcm" ) );
            } );
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
            Assert.That( result, Is.Null );
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
            Assert.That( result, Is.Null );
        }

        [Test]
        public void ParseNamespacesIni_WhenValidInput_ReturnsNamespaces()
        {
            // Arrange
            const string content = @"
[Namespaces]
Namespace1=standard
Namespace2=hk50
Namespace3=standardTSLRCM
Namespace4=hk50TSLRCM

[standard]
Name=standard hk47 no tslrcm

[hk50]
Name=hk50 no tslrcm

[standardTSLRCM]
Name=standard hk47 with tslrcm

[hk50TSLRCM]
Name=hk50 with tslrcm
";

            using (var reader = new StreamReader(CreateNamespacesIniStream(content)))
            {
                // Act
                Dictionary<string, Dictionary<string, string>> result = IniHelper.ParseNamespacesIni(reader);

                // Assert
                Assert.That( result, Is.Not.Null );
                Assert.That( result, Has.Count.EqualTo( 4 ) );
                Assert.Multiple( () =>
                {
                    Assert.That( result["standard"]["Name"], Is.EqualTo( "standard hk47 no tslrcm" ) );
                    Assert.That( result["hk50"]["Name"], Is.EqualTo( "hk50 no tslrcm" ) );
                    Assert.That( result["standardTSLRCM"]["Name"], Is.EqualTo( "standard hk47 with tslrcm" ) );
                    Assert.That( result["hk50TSLRCM"]["Name"], Is.EqualTo( "hk50 with tslrcm" ) );
                } );
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
                Assert.That( result, Is.Not.Null );
                Assert.That( result, Is.Empty );
            }
        }
    }
}
