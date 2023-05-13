// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace KOTORModSync.Core.Data
{
    public struct NWScriptHeader
    {
        public UInt32 FileType;
        public UInt32 Version;
        public UInt32 Language;
        public UInt32 NumVariables;
        public UInt32 CodeSize;
        public UInt32 NumFunctions;
        public UInt32 NumActions;
        public UInt32 NumConstants;
        public UInt32 SymbolTableSize;
        public UInt32 SymbolTableOffset;
    }

    public static class NWScriptFileReader
    {
        public static void ReadHeader(Stream stream, out NWScriptHeader header)
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
