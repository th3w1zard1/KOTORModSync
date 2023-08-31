// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core
{
    public static class ModParser
    {
	    private const string Separator = "__";
	    private static readonly Regex s_propertyRegex = new Regex(pattern: @"\*\*\w+:\*\* (.+)", RegexOptions.Compiled);

	    public static List<Component> ParseMods(string source) =>
            source.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries).Select(ParseMod).ToList();

	    private static Component ParseMod(string modText)
        {
            var mod = new Component();

            (string, string) nameAndModLinks = GetNameAndModLink(modText);
            mod.Name = GetName(nameAndModLinks);
            mod.ModLink = new List<string>
            {
                GetHyperlinkUrl( nameAndModLinks, "Name" ),
            };
            mod.Author = GetPropertyValue(modText, "Author");
            mod.Description = GetPropertyValue(modText, "Description");
            (mod.Category, mod.Tier) = GetCategoryAndTier(modText, "Category & Tier");
            //mod.NonEnglishFunctionality = GetBoolValue(modText);
            mod.InstallationMethod = GetPropertyValue(modText, "Installation Method");
            mod.Directions = GetPropertyValue(modText, "Installation Instructions");

            return mod;
        }

	    private static (string, string) GetNameAndModLink(string text)
        {
            const string pattern = @"\*\*(Name):\*\* \[([^]]+)\]\(([^)\s]+)\)(?: and \[\*\*Patch\*\*\]\(([^)\s]+)\))?";
            Match match = Regex.Match(text, pattern, RegexOptions.Singleline);

            if (!match.Success) { return (string.Empty, string.Empty); }

            string name = match.Groups[2].Value.Trim();
            string modLink = match.Groups[3].Value.Trim();
            return (name, modLink);
        }

	    private static string GetPropertyValue(string text, string propertyName)
        {
            string pattern = $@"(?i)\*\*{propertyName}:\*\* ([^_*]+)";
            Match match = Regex.Match(text, pattern, RegexOptions.Singleline);

            return !match.Success
                ? string.Empty
                : match.Groups[1].Value.Trim();
        }

	    private static string GetName((string, string) nameAndModLink) => nameAndModLink.Item1;

	    private static string GetHyperlinkUrl((string, string) nameAndModLink, string linkType) =>
            linkType.ToLower() == "name" ? nameAndModLink.Item2 : string.Empty;

	    private static (string, string) GetCategoryAndTier(string text, string categoryTierName)
        {
            string pattern = $@"(?i)\*\*{categoryTierName}:\*\* ([^_*]+)";
            Match match = Regex.Match(text, pattern, RegexOptions.Singleline);

            if (!match.Success) { return (string.Empty, string.Empty); }

            string[] values = match.Groups[1].Value.Split('/');
            if (values.Length == 2)
                return ( values[0].Trim(), values[1].Trim() );

            return (string.Empty, string.Empty);
        }

	    private static bool GetBoolValue(string text)
        {
            Match match = s_propertyRegex.Match(text);
            while (match.Success)
            {
                string value = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value.Equals( "YES", StringComparison.OrdinalIgnoreCase );

                match = match.NextMatch();
            }

            return false;
        }
    }
}
