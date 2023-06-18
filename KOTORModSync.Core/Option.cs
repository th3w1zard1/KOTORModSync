// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace KOTORModSync.Core
{
    public class Option
    {
        public string Name { get; set; }
        public List<string> Source { get; set; }
        public string Destination { get; set; }
        public List<Guid> Dependencies { get; set; }
        public List<Guid> Restrictions { get; set; }
        public Guid Guid { get; set; }
    }
}
