// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

// ReSharper disable NotAccessedField.Global

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
}
