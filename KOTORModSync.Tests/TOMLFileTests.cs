// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class TomlFileTests
    {
        private string _filePath = string.Empty;

        // ReSharper disable once ConvertToConstant.Local
        private readonly string _exampleToml = @"
            [[thisMod]]
            name = ""Ultimate Dantooine""
            guid = ""{B3525945-BDBD-45D8-A324-AAF328A5E13E}""
            dependencies = [
                ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
                ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
            ]
            installOrder = 3

            [[thisMod.instructions]]
            action = ""extract""
            source = ""Ultimate Dantooine High Resolution - TPC Version-1103-2-1-1670680013.rar""
            destination = ""%temp%\\mod_files\\Dantooine HR""
            overwrite = true

            [[thisMod.instructions]]
            action = ""delete""
            paths = [
                ""%temp%\\mod_files\\Dantooine HR\\DAN_wall03.tpc"",
                ""%temp%\\mod_files\\Dantooine HR\\DAN_NEW1.tpc"",
                ""%temp%\\mod_files\\Dantooine HR\\DAN_MWFl.tpc""
            ]

            [[thisMod.instructions]]
            action = ""move""
            source = ""%temp%\\mod_files\\Dantooine HR\\""
            destination = ""%temp%\\Override""

            [[thisMod]]
            name = ""TSLRCM Tweak Pack""
            guid = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""
            installOrder = 1
            dependencies = []

            [[thisMod.instructions]]
            action = ""extract""
            source = ""URCMTP 1.3.rar""
            destination = ""%temp%\\mod_files\\TSLRCM Tweak Pack""
            overwrite = true

            [[thisMod.instructions]]
            action = ""run""
            path = ""%temp%\\mod_files\\TSLPatcher.exe""";

        
        [SetUp]
        public void SetUp()
        {
            // Create a temporary file for testing
            _filePath = Path.GetTempFileName();

            // Write example TOMLIN content to the file
            File.WriteAllText( _filePath, _exampleToml );
        }

        [TearDown]
        public void TearDown()
        {
            // Delete the temporary file
            Assert.That( _filePath, Is.Not.Null, nameof( _filePath ) + " != null" );
            File.Delete( _filePath );
        }

        [Test]
        public void SaveAndLoadTOMLFile_MatchingComponents()
        {
            // Read the original TOMLIN file contents
            Assert.That( _filePath, Is.Not.Null, nameof( _filePath ) + " is null" );
            string tomlContents = File.ReadAllText( _filePath );

            // Fix whitespace issues
            tomlContents = Serializer.FixWhitespaceIssues( tomlContents );

            // Save the modified TOMLIN file
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText( modifiedFilePath, tomlContents );

            // Arrange
            List<Component> originalComponents = Component.ReadComponentsFromFile( modifiedFilePath );

            // Act
            Component.OutputConfigFile( originalComponents, modifiedFilePath );

            // Reload the modified TOMLIN file
            List<Component> loadedComponents = Component.ReadComponentsFromFile( modifiedFilePath );

            // Assert
            Assert.That( loadedComponents, Has.Count.EqualTo( originalComponents.Count ) );

            for ( int i = 0; i < originalComponents.Count; i++ )
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality( loadedComponent, originalComponent );
            }
        }

        [Test]
        public void SaveAndLoad_DefaultComponent()
        {
            // Deserialize default component
            Component newComponent
                = Component.DeserializeTomlComponent( _exampleToml )
                ?? throw new InvalidOperationException();
            newComponent.Guid = Guid.NewGuid();
            newComponent.Name = "test_mod_" + Path.GetRandomFileName();

            // Serialize
            string tomlString = newComponent.SerializeComponent();

            // Deserialize into new instance
            Component duplicateComponent = Component.DeserializeTomlComponent( tomlString )
                ?? throw new InvalidOperationException();

            // Compare
            AssertComponentEquality( newComponent, duplicateComponent );
        }

        [Test]
        [Ignore( "not sure if I want to support" )]
        public void SaveAndLoadTOMLFile_CaseInsensitive()
        {
            // Arrange
            List<Component> originalComponents = Component.ReadComponentsFromFile( _filePath );

            // Modify the TOML file contents
            Assert.That( _filePath, Is.Not.Null, nameof( _filePath ) + " != null" );
            string tomlContents = File.ReadAllText( _filePath );

            // Convert field names and values to mixed case
            tomlContents = ConvertFieldNamesAndValuesToMixedCase( tomlContents );

            // Act
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText( modifiedFilePath, tomlContents );

            List<Component> loadedComponents = Component.ReadComponentsFromFile( modifiedFilePath );

            // Assert
            Assert.That( loadedComponents, Has.Count.EqualTo( originalComponents.Count ) );

            for ( int i = 0; i < originalComponents.Count; i++ )
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality( originalComponent, loadedComponent );
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_WhitespaceTests()
        {
            // Arrange
            List<Component> originalComponents = Component.ReadComponentsFromFile( _filePath );

            // Modify the TOMLIN file contents
            Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
            string tomlContents = File.ReadAllText( _filePath );

            // Add mixed line endings and extra whitespaces
            tomlContents = "    \r\n\t   \r\n\r\n\r\n" + tomlContents + "    \r\n\t   \r\n\r\n\r\n";

            // Save the modified TOMLIN file
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText( modifiedFilePath, tomlContents );

            // Act
            List<Component> loadedComponents = Component.ReadComponentsFromFile( modifiedFilePath );

            // Assert
            Assert.That( loadedComponents, Has.Count.EqualTo( originalComponents.Count ) );

            for ( int i = 0; i < originalComponents.Count; i++ )
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality( originalComponent, loadedComponent );
            }
        }

        private static string ConvertFieldNamesAndValuesToMixedCase( string tomlContents )
        {
            var convertedContents = new StringBuilder();
            var random = new Random();

            bool isFieldName = true; // Flag to determine if the current item is a field name or field value

            foreach ( char c in tomlContents )
            {
                char convertedChar = c;

                if ( isFieldName )
                {
                    if ( char.IsLetter( c ) )
                    {
                        // Convert field name character to mixed case
                        convertedChar = random.Next( 2 ) == 0
                            ? char.ToUpper( c )
                            : char.ToLower( c );
                    }
                    else if ( c == ']' )
                    {
                        isFieldName = false; // Switch to field value mode after closing bracket
                    }
                }
                else
                {
                    if ( char.IsLetter( c ) )
                    {
                        // Convert field value character to mixed case
                        convertedChar = random.Next( 2 ) == 0
                            ? char.ToUpper( c )
                            : char.ToLower( c );
                    }
                    else if ( c == '[' )
                    {
                        isFieldName = true; // Switch to field name mode after opening bracket
                    }
                }

                _ = convertedContents.Append( convertedChar );
            }

            return convertedContents.ToString();
        }

        [Test]
        public void SaveAndLoadTOMLFile_EmptyComponentsList()
        {
            // Arrange
            // ReSharper disable once CollectionNeverUpdated.Local
            List<Component> originalComponents = new();
            // Act
            Component.OutputConfigFile( originalComponents, _filePath );

            try
            {
                List<Component> loadedComponents = Component.ReadComponentsFromFile( _filePath );

                // Assert
                Assert.That( loadedComponents, Is.Null.Or.Empty );
            }
            catch ( InvalidDataException )
            {
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_DuplicateGuids()
        {
            // Arrange
            List<Component> originalComponents = new()
            {
                new Component
                {
                    Name = "Component 1", Guid = Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ),
                },
                new Component
                {
                    Name = "Component 2", Guid = Guid.Parse( "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" ),
                },
                new Component
                {
                    Name = "Component 3", Guid = Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ),
                },
            };

            // Act
            Component.OutputConfigFile( originalComponents, _filePath );
            List<Component> loadedComponents = Component.ReadComponentsFromFile( _filePath );

            // Assert
            Assert.That( loadedComponents, Has.Count.EqualTo( originalComponents.Count ) );

            for ( int i = 0; i < originalComponents.Count; i++ )
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality( originalComponent, loadedComponent );
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_ModifyComponents()
        {
            // Arrange
            List<Component> originalComponents = Component.ReadComponentsFromFile( _filePath );

            // Modify some component properties
            originalComponents[0].Name = "Modified Name";

            // Act
            Component.OutputConfigFile( originalComponents, _filePath );
            List<Component> loadedComponents = Component.ReadComponentsFromFile( _filePath );

            // Assert
            Assert.That( loadedComponents, Has.Count.EqualTo( originalComponents.Count ) );

            for ( int i = 0; i < originalComponents.Count; i++ )
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality( loadedComponent, originalComponent );
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_MultipleRounds()
        {
            // Arrange
            List<List<Component>> rounds = new()
            {
                new List<Component>
                {
                    new()
                    {
                        Name = "Component 1", Guid = Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ),
                    },
                    new()
                    {
                        Name = "Component 2", Guid = Guid.Parse( "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" ),
                    },
                },
                new List<Component>
                {
                    new()
                    {
                        Name = "Component 3", Guid = Guid.Parse( "{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}" ),
                    },
                    new()
                    {
                        Name = "Component 4", Guid = Guid.Parse( "{E7B27A19-9A81-4A20-B062-7D00F2603D5C}" ),
                    },
                    new()
                    {
                        Name = "Component 5", Guid = Guid.Parse( "{F1B05F5D-3C06-4B64-8E39-8BEC8D22BB0A}" ),
                    },
                },
                new List<Component>
                {
                    new()
                    {
                        Name = "Component 6", Guid = Guid.Parse( "{EF04A28E-5031-4A95-A85A-9A1B29A31710}" ),
                    },
                    new()
                    {
                        Name = "Component 7", Guid = Guid.Parse( "{B0373F49-ED5A-43A1-91E0-5CEB85659282}" ),
                    },
                    new()
                    {
                        Name = "Component 8", Guid = Guid.Parse( "{BBDB9C8D-DA44-4859-A641-0364D6F34D12}" ),
                    },
                    new()
                    {
                        Name = "Component 9", Guid = Guid.Parse( "{D6B5C60F-26A7-4595-A0E2-2DE567A376DE}" ),
                    },
                },
            };
            // Act and Assert
            foreach ( List<Component> components in rounds )
            {
                Component.OutputConfigFile( components, _filePath );
                List<Component> loadedComponents = Component.ReadComponentsFromFile( _filePath );

                Assert.That( loadedComponents, Has.Count.EqualTo( components.Count ) );

                for ( int i = 0; i < components.Count; i++ )
                {
                    Component originalComponent = components[i];
                    Component loadedComponent = loadedComponents[i];

                    AssertComponentEquality( originalComponent, loadedComponent );
                }
            }
        }

        [Test]
        public void TomlWriteStringTest()
        {
            // Sample nested Dictionary representing TOML data
            var innerDictionary1 = new Dictionary<string, object>
            {
                {
                    "name", "John"
                },
                {
                    "age", 30
                },
                // other key-value pairs for the first table
            };

            var innerDictionary2 = new Dictionary<string, object>
            {
                {
                    "name", "Alice"
                },
                {
                    "age", 25
                },
                // other key-value pairs for the second table
            };

            var rootTable = new Dictionary<string, object>
            {
                {
                    "thisMod", new List<object>
                    {
                        innerDictionary1, innerDictionary2,
                        // additional dictionaries in the list
                    }
                },
            };

            Logger.Log( TomlWriter.WriteString( rootTable ) );
            Logger.Log( Tomlyn.Toml.FromModel( rootTable ) );
        }

        private static void AssertComponentEquality( object? obj, object? another )
        {
            if ( ReferenceEquals( obj, another ) )
                return;
            if ( obj is null || another is null )
                return;
            if ( obj.GetType() != another.GetType() )
                return;

            string objJson = JsonConvert.SerializeObject( obj );
            string anotherJson = JsonConvert.SerializeObject( another );

            Assert.That( objJson, Is.EqualTo( anotherJson ) );
        }
    }
}
