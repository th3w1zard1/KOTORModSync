// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using IniParser.Model;

namespace KOTORModSync.Core.Utility
{
    public static class IniFileMerger
    {
        public static IniData MergeIniFiles( IniData iniData1, IniData iniData2 )
        {
            var mergedIniData = new IniData();

            // Merge sections and keys from iniData1
            foreach ( SectionData section in iniData1.Sections )
            {
                _ = mergedIniData.Sections.AddSection( section.SectionName );
                foreach ( KeyData key in section.Keys )
                {
                    _ = mergedIniData[section.SectionName].AddKey( key.KeyName, key.Value );
                }
            }

            // Merge sections and keys from iniData2
            foreach ( SectionData section in iniData2.Sections )
            {
                // If the section already exists in iniData1, append a number to the section name
                string mergedSectionName = section.SectionName;
                for ( int sectionNumber = 1;
                     mergedIniData.Sections.ContainsSection( mergedSectionName );
                     sectionNumber++ )
                {
                    mergedSectionName = section.SectionName + sectionNumber;
                }

                _ = mergedIniData.Sections.AddSection( mergedSectionName );
                foreach ( KeyData key in section.Keys )
                {
                    _ = mergedIniData[mergedSectionName].AddKey( key.KeyName, key.Value );
                }
            }

            return mergedIniData;
        }
    }
}
