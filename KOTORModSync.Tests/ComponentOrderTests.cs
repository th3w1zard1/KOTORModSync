﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                new Component { Name = "C1_InstallBefore_C2", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid>{thisGuid} },
                new Component { Name = "C2", Guid = thisGuid, InstallAfter = null, InstallBefore = null },
                new Component { Name = "C3", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( componentsListExpectedOrder );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.True );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsFalse()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<Component>
            {
                new Component { Name = "C2", Guid = thisGuid, InstallAfter = null, InstallBefore = null },
                new Component { Name = "C1_InstallBefore_C2", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid>{thisGuid} },
                new Component { Name = "C3", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( unorderedList );

            // Create a copy of unorderedList with the expected order
            var componentsListExpectedOrder = new List<Component>( unorderedList );
            Swap( componentsListExpectedOrder, 0, 1 );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.False );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsTrue()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var componentsListExpectedOrder = new List<Component>
            {
                new Component { Name = "C1", Guid = thisGuid, InstallAfter = null, InstallBefore = null },
                new Component { Name = "C2_InstallAfter_C1", Guid = Guid.NewGuid(), InstallAfter = new List<Guid>{thisGuid}, InstallBefore = null },
                new Component { Name = "C3", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( componentsListExpectedOrder );
            
            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.True );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsFalse()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<Component>
            {
                new Component { Name = "C1_InstallAfter_C2", Guid = Guid.NewGuid(), InstallAfter = new List<Guid>{thisGuid}, InstallBefore = null },
                new Component { Name = "C2", Guid = thisGuid, InstallAfter = null, InstallBefore = null },
                new Component { Name = "C3", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( unorderedList );

            // Create a copy of unorderedList with the expected order
            var componentsListExpectedOrder = new List<Component>( unorderedList );
            Swap( componentsListExpectedOrder, 0, 1 );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsListExpectedOrder.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.False );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_CorrectOrder()
        {
            // Arrange
            var componentA = new Component { Name = "A", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null };
            var componentB = new Component { Name = "B", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentA.Guid }, InstallBefore = null };
            var componentC = new Component { Name = "C", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentA.Guid } };
            var componentD = new Component { Name = "D", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentB.Guid } };
            var componentFGuid = new Guid();
            var componentE = new Component { Name = "E", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentB.Guid }, InstallBefore = new List<Guid> { componentFGuid } };
            var componentF = new Component { Name = "F", Guid = componentFGuid, InstallAfter = new List<Guid> { componentE.Guid, componentB.Guid }, InstallBefore = null };
            var componentG = new Component { Name = "G", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentD.Guid, componentF.Guid }, InstallBefore = null };
            var componentH = new Component { Name = "H", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentG.Guid } };
            var componentI = new Component { Name = "I", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentG.Guid } };
            var componentJ = new Component { Name = "J", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentH.Guid, componentI.Guid }, InstallBefore = null };

            var correctOrderedComponentsList = new List<Component>
            {
                componentC, componentD, componentA, componentB, componentE,
                componentF, componentH, componentI, componentG, componentJ
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( correctOrderedComponentsList );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = correctOrderedComponentsList.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.True );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_Unordered()
        {
            // Arrange
            var componentA = new Component { Name = "A", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null };
            var componentB = new Component { Name = "B", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentA.Guid }, InstallBefore = null };
            var componentC = new Component { Name = "C", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentA.Guid } };
            var componentD = new Component { Name = "D", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentB.Guid } };
            var componentFGuid = new Guid();
            var componentE = new Component { Name = "E", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentB.Guid }, InstallBefore = new List<Guid> { componentFGuid } };
            var componentF = new Component { Name = "F", Guid = componentFGuid, InstallAfter = new List<Guid> { componentE.Guid, componentB.Guid }, InstallBefore = null };
            var componentG = new Component { Name = "G", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentD.Guid, componentF.Guid }, InstallBefore = null };
            var componentH = new Component { Name = "H", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentG.Guid } };
            var componentI = new Component { Name = "I", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { componentG.Guid } };
            var componentJ = new Component { Name = "J", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentH.Guid, componentI.Guid }, InstallBefore = null };

            var unorderedComponentsList = new List<Component>
            {
                componentA, componentB, componentC, componentD, componentE,
                componentF, componentG, componentH, componentI, componentJ
            };
            var correctOrderedComponentsList = new List<Component>
            {
                componentC, componentA, componentD, componentB, componentE,
                componentF, componentH, componentI, componentG, componentJ
            };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( unorderedComponentsList );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = correctOrderedComponentsList.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.False );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ImpossibleScenario_ReturnsFalse()
        {
            // Arrange
            var componentA = new Component { Name = "A", Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { Guid.NewGuid() } };
            var componentB = new Component { Name = "B", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentA.Guid }, InstallBefore = null };
            var componentC = new Component { Name = "C", Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { componentB.Guid }, InstallBefore = new List<Guid> { componentA.Guid } };

            var componentsList = new List<Component> { componentA, componentB, componentC };

            // Act
            (bool isCorrectOrder, List<Component> reorderedComponents) = Component.ConfirmComponentsInstallOrder( componentsList );

            // Assert
            foreach ( Component component in reorderedComponents )
            {
                int actualIndex = reorderedComponents.FindIndex( c => c.Guid == component.Guid );
                int expectedIndex = componentsList.FindIndex( c => c.Guid == component.Guid );
                Assert.That( actualIndex, Is.EqualTo( expectedIndex ), $"Component {component.Name} is out of order." );
            }

            Assert.Multiple( () =>
            {
                Assert.That( isCorrectOrder, Is.False );
                Assert.That( reorderedComponents, Is.Not.Empty );
            } );
        }


        private static void Swap<T>( IList<T> list, int index1, int index2 ) => (list[index1], list[index2]) = (list[index2], list[index1]);
    }
}