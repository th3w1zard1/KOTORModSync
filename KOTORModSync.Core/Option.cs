// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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
