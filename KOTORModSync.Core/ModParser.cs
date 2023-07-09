﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    public static class ModParser
    {
        private const string Separator = "__";

        public static List<Component> ParseMods( string source ) => source
            .Split( new[] { Separator }, StringSplitOptions.RemoveEmptyEntries )
            .Select( ParseMod )
            .ToList();

        private static Component ParseMod( [CanBeNull] string modText )
        {
            var mod = new Component();

            (string, string) nameAndModLinks = GetNameAndModLink( modText );
            mod.Name = GetName( nameAndModLinks );
            mod.ModLink = GetHyperlinkUrl( nameAndModLinks, "Name" );
            mod.Author = GetPropertyValue( modText, "Author" );
            mod.Description = GetPropertyValue( modText, "Description" );
            (mod.Category, mod.Tier) = GetCategoryAndTier( modText, "Category & Tier" );
            mod.NonEnglishFunctionality = GetBoolValue( modText, "Non-English Functionality" );
            mod.InstallationMethod = GetPropertyValue( modText, "Installation Method" );
            mod.Directions = GetPropertyValue( modText, "Installation Instructions" );

            return mod;
        }

        private static (string, string) GetNameAndModLink( string text )
        {
            const string pattern = @"\*\*(Name):\*\* \[([^]]+)\]\(([^)\s]+)\)(?: and \[\*\*Patch\*\*\]\(([^)\s]+)\))?";
            Match match = Regex.Match( text, pattern, RegexOptions.Singleline );

            if ( !match.Success )
            {
                return (string.Empty, string.Empty);
            }

            string name = match.Groups[2].Value.Trim();
            string modLink = match.Groups[3].Value.Trim();
            return (name, modLink);
        }

        private static string GetPropertyValue( string text, [CanBeNull] string propertyName )
        {
            string pattern = $@"(?i)\*\*{propertyName}:\*\* ([^_*]+)";
            Match match = Regex.Match( text, pattern, RegexOptions.Singleline );

            return !match.Success
                ? string.Empty
                : match.Groups[1].Value.Trim();
        }

        private static string GetName( (string, string) nameAndModLink ) => nameAndModLink.Item1;

        private static string GetHyperlinkUrl( (string, string) nameAndModLink, [CanBeNull] string linkType ) =>
            string.Equals( linkType, "name", StringComparison.OrdinalIgnoreCase )
                ? nameAndModLink.Item2
                : string.Empty;

        private static (string, string) GetCategoryAndTier( string text, [CanBeNull] string categoryTierName )
        {
            string pattern = $@"(?i)\*\*{categoryTierName}:\*\* ([^_*]+)";
            Match match = Regex.Match( text, pattern, RegexOptions.Singleline );

            if ( !match.Success )
            {
                return (string.Empty, string.Empty);
            }

            string[] values = match.Groups[1].Value.Split( '/' );
            return values.Length == 2
                ? (values[0].Trim(), values[1].Trim())
                : (string.Empty, string.Empty);
        }

        private static bool GetBoolValue( string text, [CanBeNull] string propertyName )
        {
            string pattern = $@"\*\*{propertyName}:\*\* (.+)";
            Match match = Regex.Match( text, pattern );
            while ( match.Success )
            {
                string value = match.Groups[1].Value.Trim();
                if ( !string.IsNullOrWhiteSpace( value ) )
                {
                    return value.Equals( "YES", StringComparison.OrdinalIgnoreCase );
                }

                match = match.NextMatch();
            }

            return false;
        }
    }
}
