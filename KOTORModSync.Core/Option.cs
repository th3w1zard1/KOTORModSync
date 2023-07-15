// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    public class Option
    {
        [NotNull] public string Name { get; set; } = string.Empty;
        [NotNull] public Guid Guid { get; set; } = Guid.Empty;
        [NotNull] public List<Guid> Dependencies { get; set; } = new List<Guid>();
        [NotNull] public List<Guid> Restrictions { get; set; } = new List<Guid>();
        [NotNull][ItemNotNull] public List<Instruction> Instructions { get; set; } = new List<Instruction>();
    }
}
