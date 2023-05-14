// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KOTORModSync.GUI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<Component> Components { get; } = new ObservableCollection<Component>();

        public MainViewModel()
        {
            var componentA = new Component { Name = "Component A" };
            var componentB = new Component { Name = "Component B" };
            var componentC = new Component { Name = "Component C" };
            var componentD = new Component { Name = "Component D" };
            var componentE = new Component { Name = "Component E" };

            componentB.Dependencies = new List<Component> { componentA };
            componentC.Dependencies = new List<Component> { componentA, componentB };
            componentD.Dependencies = new List<Component> { componentB };
            componentE.Dependencies = new List<Component> { componentD };

            Components.Add(componentA);
            Components.Add(componentB);
            Components.Add(componentC);
            Components.Add(componentD);
            Components.Add(componentE);
        }
    }
}
