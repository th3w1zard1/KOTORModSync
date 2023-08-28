// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

namespace KOTORModSync.Core.Data
{
    public static class Game
    {
	    public static readonly List<string> TextureOverridePriorityList = new List<string>
        {
            ".dds", ".tpc", ".tga",
        };
    }
}
