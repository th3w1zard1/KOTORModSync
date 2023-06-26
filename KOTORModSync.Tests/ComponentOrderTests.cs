// Licensed to the .NET Foundation under one or more agreements.
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
        public void ConfirmComponentsInstallOrder_AllComponentsInCorrectOrder_Success()
        {
            // Arrange
            var componentsList = new List<Component>
            {
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null },
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null },
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, _) = Component.ConfirmComponentsInstallOrder( componentsList );

            // Assert
            Assert.That( isCorrectOrder, Is.True );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComponentOrderIncorrect_ReturnsFalse()
        {
            // Arrange
            var thisGuid = Guid.NewGuid();
            var componentsList = new List<Component>
            {
                new() { Guid = thisGuid, InstallAfter = null, InstallBefore = null },
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null },
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid>{thisGuid} }
            };

            // Swap the order of the first two components
            Swap( componentsList, 0, 1 );

            // Act
            (bool isCorrectOrder, _) = Component.ConfirmComponentsInstallOrder( componentsList );

            // Assert
            Assert.That( isCorrectOrder, Is.False );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComponentWithInstallAfterDependency()
        {
            // Arrange
            var component1 = new Component { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null };
            var component2 = new Component { Guid = Guid.NewGuid(), InstallAfter = new List<Guid> { component1.Guid }, InstallBefore = null };
            var componentsList = new List<Component>
            {
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null },
                component1,
                component2,
                new() { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null }
            };

            // Act
            (bool isCorrectOrder, _) = Component.ConfirmComponentsInstallOrder( componentsList );

            // Assert
            Assert.That( isCorrectOrder, Is.True );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComponentWithInstallBeforeDependency()
        {
            // Arrange
            var component1 = new Component { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = null };
            var component2 = new Component { Guid = Guid.NewGuid(), InstallAfter = null, InstallBefore = new List<Guid> { component1.Guid } };
            var componentsList = new List<Component> { component1, component2 };

            // Act
            (bool isCorrectOrder, _) = Component.ConfirmComponentsInstallOrder( componentsList );

            // Assert
            Assert.That( isCorrectOrder, Is.False );
        }

        private static void Swap<T>( IList<T> list, int index1, int index2 ) => (list[index1], list[index2]) = (list[index2], list[index1]);
    }
}
