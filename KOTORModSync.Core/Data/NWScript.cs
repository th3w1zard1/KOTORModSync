// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public static void ReadHeader([NotNull] Stream stream, out NWScriptHeader header)
        {
            BinaryReader reader = new BinaryReader(stream);

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