// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.IO;
using JetBrains.Annotations;
// ReSharper disable UnusedType.Global

namespace KOTORModSync.Core.Data
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public static class NWScriptFileReader
    {
        // ReSharper disable once UnusedMember.Global
        public static void ReadHeader( [NotNull] Stream stream, out NWScriptHeader header )
        {
            var reader = new BinaryReader( stream );

            header.FileType = reader.ReadUInt32();
            header.Version = reader.ReadUInt32();
            header.Language = reader.ReadUInt32();
            header.NumVariables = reader.ReadUInt32();
            header.CodeSize = reader.ReadUInt32();
            header.NumFunctions = reader.ReadUInt32();
            header.NumActions = reader.ReadUInt32();
            header.NumConstants = reader.ReadUInt32();
            header.SymbolTableSize = reader.ReadUInt32();
            header.SymbolTableOffset = reader.ReadUInt32();
        }
    }
}
