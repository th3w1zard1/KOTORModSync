using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using System.Text;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class TOMLFileTests
    {
        private string filePath;
        private string exampleTOML = @"
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
            path = ""%temp%\\mod_files\\TSLPatcher.exe""
            arguments = """"";

        [SetUp]
        public void SetUp()
        {
            // Create a temporary file for testing
            filePath = Path.GetTempFileName();

            // Write example TOML content to the file
            File.WriteAllText(filePath, exampleTOML);
        }

        [TearDown]
        public void TearDown()
        {
            // Delete the temporary file
            File.Delete(filePath);
        }

        [Test]
        public void SaveAndLoadTOMLFile_MatchingComponents()
        {
            // Read the original TOML file contents
            string tomlContents = File.ReadAllText(filePath);

            // Fix whitespace issues
            tomlContents = Serializer.FixWhitespaceIssues(tomlContents);

            // Save the modified TOML file
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText(modifiedFilePath, tomlContents);

            // Arrange
            List<Component> originalComponents = Serializer.FileHandler.ReadComponentsFromFile(modifiedFilePath);

            // Act
            Serializer.FileHandler.OutputConfigFile(originalComponents, modifiedFilePath);

            // Reload the modified TOML file
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(modifiedFilePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }

        // not sure if I want to support this.
        [Test]
        public void SaveAndLoadTOMLFile_CaseInsensitive()
        {
            // Arrange
            List<Component> originalComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Modify the TOML file contents
            string tomlContents = File.ReadAllText(filePath);

            // Convert field names and values to mixed case
            tomlContents = ConvertFieldNamesAndValuesToMixedCase(tomlContents);

            // Act
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText(modifiedFilePath, tomlContents);

            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(modifiedFilePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }


        [Test]
        public void SaveAndLoadTOMLFile_WhitespaceTests()
        {
            // Arrange
            List<Component> originalComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Modify the TOML file contents
            string tomlContents = File.ReadAllText(filePath);

            // Add mixed line endings and extra whitespaces
            tomlContents += "    \r\n\t   \r\n\r\n\r\n";

            // Save the modified TOML file
            string modifiedFilePath = Path.GetTempFileName();
            File.WriteAllText(modifiedFilePath, tomlContents);

            // Act
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(modifiedFilePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }
        private static string ConvertFieldNamesAndValuesToMixedCase(string tomlContents)
        {
            var convertedContents = new StringBuilder(10000);
            var random = new Random();

            bool isFieldName = true; // Flag to determine if the current item is a field name or field value

            foreach (char c in tomlContents)
            {
                char convertedChar = c;

                if (isFieldName)
                {
                    if (char.IsLetter(c))
                    {
                        // Convert field name character to mixed case
                        if (random.Next(2) == 0)
                            convertedChar = char.ToUpper(c);
                        else
                            convertedChar = char.ToLower(c);
                    }
                    else if (c == ']')
                    {
                        isFieldName = false; // Switch to field value mode after closing bracket
                    }
                }
                else
                {
                    if (char.IsLetter(c))
                    {
                        // Convert field value character to mixed case
                        if (random.Next(2) == 0)
                            convertedChar = char.ToUpper(c);
                        else
                            convertedChar = char.ToLower(c);
                    }
                    else if (c == '[')
                    {
                        isFieldName = true; // Switch to field name mode after opening bracket
                    }
                }

                convertedContents.Append(convertedChar);
            }

            return convertedContents.ToString();
        }




        [Test]
        public void SaveAndLoadTOMLFile_EmptyComponentsList()
        {
            // Arrange
            List<Component> originalComponents = new List<Component>();
            // Act
            Serializer.FileHandler.OutputConfigFile(originalComponents, filePath);
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Assert
            Assert.IsEmpty(loadedComponents);
        }

        [Test]
        public void SaveAndLoadTOMLFile_DuplicateGuids()
        {
            // Arrange
            List<Component> originalComponents = new List<Component>
            {
                new Component { Name = "Component 1", Guid = "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" },
                new Component { Name = "Component 2", Guid = "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" },
                new Component { Name = "Component 3", Guid = "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" },
            };

            // Act
            Serializer.FileHandler.OutputConfigFile(originalComponents, filePath);
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_MissingRequiredFields()
        {
            // Arrange
            List<Component> originalComponents = new List<Component>
        {
            new Component { Name = "Component 1" },
            new Component { Guid = "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" },
            new Component { InstallOrder = 3 },
        };

            // Act
            Serializer.FileHandler.OutputConfigFile(originalComponents, filePath);
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }

        [Test]
        public void SaveAndLoadTOMLFile_ModifyComponents()
        {
            // Arrange
            List<Component> originalComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Modify some component properties
            originalComponents[0].Name = "Modified Name";
            originalComponents[1].InstallOrder = 5;

            // Act
            Serializer.FileHandler.OutputConfigFile(originalComponents, filePath);
            List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

            // Assert
            Assert.That(loadedComponents.Count, Is.EqualTo(originalComponents.Count));

            for (int i = 0; i < originalComponents.Count; i++)
            {
                Component originalComponent = originalComponents[i];
                Component loadedComponent = loadedComponents[i];

                AssertComponentEquality(originalComponent, loadedComponent);
            }
        }
        [Test]
        public void SaveAndLoadTOMLFile_MultipleRounds()
        {
            // Arrange
            List<List<Component>> rounds = new List<List<Component>>
            {
                new List<Component>
                {
                    new Component { Name = "Component 1", Guid = "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" },
                    new Component { Name = "Component 2", Guid = "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" },
                },
                new List<Component>
                {
                    new Component { Name = "Component 3", Guid = "{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}" },
                    new Component { Name = "Component 4", Guid = "{E7B27A19-9A81-4A20-B062-7D00F2603D5C}" },
                    new Component { Name = "Component 5", Guid = "{F1B05F5D-3C06-4B64-8E39-8BEC8D22BB0A}" },
                },
                new List<Component>
                {
                    new Component { Name = "Component 6", Guid = "{EF04A28E-5031-4A95-A85A-9A1B29A31710}" },
                    new Component { Name = "Component 7", Guid = "{B0373F49-ED5A-43A1-91E0-5CEB85659282}" },
                    new Component { Name = "Component 8", Guid = "{BBDB9C8D-DA44-4859-A641-0364D6F34D12}" },
                    new Component { Name = "Component 9", Guid = "{D6B5C60F-26A7-4595-A0E2-2DE567A376DE}" },
                }
            };
            // Act and Assert
            foreach (List<Component> components in rounds)
            {
                Serializer.FileHandler.OutputConfigFile(components, filePath);
                List<Component> loadedComponents = Serializer.FileHandler.ReadComponentsFromFile(filePath);

                Assert.That(loadedComponents.Count, Is.EqualTo(components.Count));

                for (int i = 0; i < components.Count; i++)
                {
                    Component originalComponent = components[i];
                    Component loadedComponent = loadedComponents[i];

                    AssertComponentEquality(originalComponent, loadedComponent);
                }
            }
        }

        private static void AssertComponentEquality(Component expected, Component actual)
        {
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Guid, Is.EqualTo(expected.Guid));
            Assert.That(actual.InstallOrder, Is.EqualTo(expected.InstallOrder));
            // Add assertions for other fields if necessary
        }
    }
}
