// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;

namespace KOTORModSync.Tests
{
    [TestFixture]
    internal class ComponentOrderTests
    {
        [Test]
        public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsTrue()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var componentsListExpectedOrder = new List<Component>
            {
                new()
                {
                    Name = "C1_InstallBefore_C2",
                    Guid = Guid.NewGuid(),
                    InstallBefore = new List<Guid> { thisGuid },
                },
                new() {Name = "C2", Guid = thisGuid},
                new() {Name = "C3", Guid = Guid.NewGuid()},
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( componentsListExpectedOrder );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.True );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsFalse()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<Component>
            {
                new() {Name = "C2", Guid = thisGuid},
                new()
                {
                    Name = "C1_InstallBefore_C2",
                    Guid = Guid.NewGuid(),
                    InstallBefore = new List<Guid> { thisGuid },
                },
                new() {Name = "C3", Guid = Guid.NewGuid()},
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( unorderedList );

            // Create a copy of unorderedList with the expected order
            var componentsListExpectedOrder = new List<Component>( unorderedList );
            Swap( componentsListExpectedOrder, index1: 0, index2: 1 );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.False );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsTrue()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var componentsListExpectedOrder = new List<Component>
            {
                new() {Name = "C1", Guid = thisGuid},
                new()
                {
                    Name = "C2_InstallAfter_C1",
                    Guid = Guid.NewGuid(),
                    InstallAfter = new List<Guid> { thisGuid },
                },
                new() {Name = "C3", Guid = Guid.NewGuid()},
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( componentsListExpectedOrder );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.True );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsFalse()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<Component>
            {
                new()
                {
                    Name = "C1_InstallAfter_C2",
                    Guid = Guid.NewGuid(),
                    InstallAfter = new List<Guid> { thisGuid },
                },
                new() { Name = "C2", Guid = thisGuid },
                new() {Name = "C3", Guid = Guid.NewGuid()},
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( unorderedList );

            // Create a copy of unorderedList with the expected order
            var componentsListExpectedOrder = new List<Component>( unorderedList );
            Swap( componentsListExpectedOrder, index1: 0, index2: 1 );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.False );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_CorrectOrder()
        {
            // Arrange
            var componentA = new Component
            {
                Name = "A",
                Guid = Guid.NewGuid(),
            };
            var componentB = new Component
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentA.Guid },
            };
            var componentC = new Component
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentA.Guid },
            };
            var componentD = new Component
            {
                Name = "D",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentB.Guid },
            };
            var componentFGuid = new Guid();
            var componentE = new Component
            {
                Name = "E",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentB.Guid },
                InstallBefore = new List<Guid> { componentFGuid },
            };
            var componentF = new Component
            {
                Name = "F",
                Guid = componentFGuid,
                InstallAfter = new List<Guid> { componentE.Guid, componentB.Guid },
            };
            var componentG = new Component
            {
                Name = "G",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentD.Guid, componentF.Guid },
            };
            var componentH = new Component
            {
                Name = "H",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentG.Guid },
            };
            var componentI = new Component
            {
                Name = "I",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentG.Guid },
            };
            var componentJ = new Component
            {
                Name = "J",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentH.Guid, componentI.Guid },
            };

            var correctOrderedComponentsList = new List<Component>
            {
                componentC,
                componentD,
                componentA,
                componentB,
                componentE,
                componentF,
                componentH,
                componentI,
                componentG,
                componentJ,
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( correctOrderedComponentsList );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = correctOrderedComponentsList.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.True );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_Unordered()
        {
            // Arrange
            var componentA = new Component
            {
                Name = "A",
                Guid = Guid.NewGuid(),
            };
            var componentB = new Component
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentA.Guid },
            };
            var componentC = new Component
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentA.Guid },
            };
            var componentD = new Component
            {
                Name = "D",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentB.Guid },
            };
            var componentFGuid = new Guid();
            var componentE = new Component
            {
                Name = "E",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentB.Guid },
                InstallBefore = new List<Guid> { componentFGuid },
            };
            var componentF = new Component
            {
                Name = "F",
                Guid = componentFGuid,
                InstallAfter = new List<Guid> { componentE.Guid, componentB.Guid },
            };
            var componentG = new Component
            {
                Name = "G",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentD.Guid, componentF.Guid },
            };
            var componentH = new Component
            {
                Name = "H",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { componentG.Guid },
            };
            var componentI = new Component
            {
                Name = "I",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentH.Guid },
                InstallBefore = new List<Guid> { componentG.Guid },
            };
            var componentJ = new Component
            {
                Name = "J",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentH.Guid, componentI.Guid },
            };

            var unorderedComponentsList = new List<Component>
            {
                componentA,
                componentB,
                componentC,
                componentD,
                componentE,
                componentF,
                componentG,
                componentH,
                componentI,
                componentJ,
            };
            var correctOrderedComponentsList = new List<Component>
            {
                componentC,
                componentA,
                componentD,
                componentB,
                componentE,
                componentF,
                componentH,
                componentI,
                componentG,
                componentJ,
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents)
                = Component.ConfirmComponentsInstallOrder( unorderedComponentsList );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = correctOrderedComponentsList.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component '{component.Name}' is out of order." );
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That( isCorrectOrder, Is.False );
                    Assert.That( reorderedComponents, Is.Not.Empty );
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ImpossibleScenario_ReturnsFalse()
        {
            // Arrange
            var componentA = new Component
            {
                Name = "A",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { Guid.NewGuid() },
            };
            var componentB = new Component
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentA.Guid },
            };
            var componentC = new Component
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { componentB.Guid },
                InstallBefore = new List<Guid> { componentA.Guid },
            };

            var componentsList = new List<Component> { componentA, componentB, componentC };

            // Act
            try
            {
                _ = Component.ConfirmComponentsInstallOrder( componentsList );
            }
            // Assert
            catch ( KeyNotFoundException )
            {
                return;
            }

            Assert.That(
                actual: false,
                Is.True,
                message: "ConfirmComponentsInstallOrder should have raised a KeyNotFoundException"
            );
        }

        private static void Swap<T>( IList<T> list, int index1, int index2 ) =>
            (list[index1], list[index2]) = (list[index2], list[index1]);
    }
}
