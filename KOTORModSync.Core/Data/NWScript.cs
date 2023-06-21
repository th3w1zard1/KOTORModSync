// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.IO;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Data
{
    // ReSharper disable once InconsistentNaming
    public struct NWScriptHeader
    {
        public uint FileType;
        public uint Version;
        public uint Language;
        public uint NumVariables;
        public uint CodeSize;
        public uint NumFunctions;
        public uint NumActions;
        public uint NumConstants;
        public uint SymbolTableSize;
        public uint SymbolTableOffset;
    }

    // ReSharper disable once InconsistentNaming
    public static class NWScriptFileReader
    {
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
