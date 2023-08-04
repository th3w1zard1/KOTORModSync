// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using IniParser.Model;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    public static class IniFileMerger
    {
        [NotNull]
        public static IniData MergeIniFiles( [NotNull] IniData iniData1, [NotNull] IniData iniData2 )
        {
            if ( iniData1 == null )
                throw new ArgumentNullException( nameof( iniData1 ) );
            if ( iniData2 == null )
                throw new ArgumentNullException( nameof( iniData2 ) );

            var mergedIniData = new IniData();

            // Merge sections and keys from iniData1
            foreach ( SectionData section in iniData1.Sections )
            {
                _ = mergedIniData.Sections.AddSection( section.SectionName );
                foreach ( KeyData key in section.Keys )
                {
                    _ = mergedIniData[section.SectionName]
                        .AddKey( key.KeyName, key.Value );
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
                    _ = mergedIniData[mergedSectionName]
                        .AddKey( key.KeyName, key.Value );
                }
            }

            return mergedIniData;
        }
    }
}
