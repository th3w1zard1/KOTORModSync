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
		private const string Separator = "\n\n\n";
		private static readonly Regex s_modNameRegex = new Regex(
			pattern: @"^(.+?) - by (.+)$",
			RegexOptions.Compiled | RegexOptions.Multiline
		);
		private static readonly Regex s_propertyRegex = new Regex(
			pattern: @"\*\*(Type|Tier|Rank|Installation Instructions):\*\* (.+)",
			RegexOptions.Compiled | RegexOptions.Multiline
		);
		private static readonly Regex s_descriptionRegex = new Regex(
			pattern: @"(?<=\n)(?!Type:|Tier:|Rank:|Installation Instructions:).+",
			RegexOptions.Compiled | RegexOptions.Singleline
		);

		private static readonly Regex s_modSectionRegex = new Regex(
			pattern: @"(.*?) - by (.*?)\nType: (.*?)\n(Rank|Tier): (.*?)\nInstallation Instructions: (.*?)\n\t(.*?)\n",
			RegexOptions.Compiled | RegexOptions.Singleline
		);

		public static List<Component> ParseMods(string source)
		{
			MatchCollection modSections = new Regex(
				pattern: @"^(.*?)\r?\nType:\s*(.*?)\r?\n(Rank:\s*(.*?)\r?\n)?(Installation Instructions:\s*(.*?))?\r?\n((?:\t.*\r?\n)+)",
				RegexOptions.Compiled | RegexOptions.Multiline
			).Matches(source);
			var mods = new List<Component>();

			foreach (Match modSection in modSections)
			{
				var mod = new Component
				{
					Name = modSection.Groups[1].Value.Trim(),
					Guid = Guid.NewGuid(),
					Category = modSection.Groups[2].Value.Trim(),
					Tier = modSection.Groups[4].Value.Trim(),
					Directions = modSection.Groups[6].Value.Trim(),
					Description = modSection.Groups[7].Value.Trim(),
				};
				mods.Add(mod);
			}

			return mods;
		}

		private static Component ParseMod(string modText)
		{
			var mod = new Component();

			// Extract mod name and author
			Match nameAuthorMatch = s_modNameRegex.Match(modText);
			if (nameAuthorMatch.Success)
			{
				mod.Name = nameAuthorMatch.Groups[1].Value.Trim();
				mod.Author = nameAuthorMatch.Groups[2].Value.Trim();
			}

			// Extract properties
			MatchCollection propertyMatches = s_propertyRegex.Matches(modText);
			foreach (Match match in propertyMatches)
			{
				string propertyName = match.Groups[1].Value.Trim();
				string propertyValue = match.Groups[2].Value.Trim();

				switch (propertyName)
				{
					case "Type":
						mod.Category = propertyValue;
						break;
					case "Tier":
					case "Rank":
						mod.Tier = propertyValue;
						break;
					case "Installation Instructions":
						mod.Directions = propertyValue;
						break;
				}
			}

			// Extract description
			Match descriptionMatch = s_descriptionRegex.Match(modText);
			if (descriptionMatch.Success)
			{
				mod.Description = descriptionMatch.Value.Trim();
			}

			return mod;
		}
	}
	public static class RedditModParser
	{
		private const string Separator = "__";
		private static readonly Regex s_propertyRegex = new Regex(pattern: @"\*\*\w+:\*\* (.+)", RegexOptions.Compiled);

		public static List<Component> ParseMods(string source) =>
			source.Split(
				new[]
				{
					Separator,
				},
				StringSplitOptions.RemoveEmptyEntries
			).Select(ParseMod).ToList();

		private static Component ParseMod(string modText)
		{
			var mod = new Component();

			(string, string) nameAndModLinks = GetNameAndModLink(modText);
			mod.Name = GetName(nameAndModLinks);
			mod.ModLink = new List<string>
			{
				GetHyperlinkUrl(nameAndModLinks, linkType: "Name"),
			};
			mod.Author = GetPropertyValue(modText, propertyName: "Author");
			mod.Description = GetPropertyValue(modText, propertyName: "Description");
			(mod.Category, mod.Tier) = GetCategoryAndTier(modText, categoryTierName: "Category & Tier");
			//mod.NonEnglishFunctionality = GetBoolValue(modText);
			mod.InstallationMethod = GetPropertyValue(modText, propertyName: "Installation Method");
			mod.Directions = GetPropertyValue(modText, propertyName: "Installation Instructions");

			return mod;
		}

		private static (string, string) GetNameAndModLink(string text)
		{
			const string pattern = @"\*\*(Name):\*\* \[([^]]+)\]\(([^)\s]+)\)(?: and \[\*\*Patch\*\*\]\(([^)\s]+)\))?";
			Match match = Regex.Match(text, pattern, RegexOptions.Singleline);

			if ( !match.Success )
			{
				return (string.Empty, string.Empty);
			}

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
			linkType.ToLower() == "name"
				? nameAndModLink.Item2
				: string.Empty;

		private static (string, string) GetCategoryAndTier(string text, string categoryTierName)
		{
			string pattern = $@"(?i)\*\*{categoryTierName}:\*\* ([^_*]+)";
			Match match = Regex.Match(text, pattern, RegexOptions.Singleline);

			if ( !match.Success )
			{
				return (string.Empty, string.Empty);
			}

			string[] values = match.Groups[1].Value.Split('/');
			if ( values.Length == 2 )
				return (values[0].Trim(), values[1].Trim());

			return (string.Empty, string.Empty);
		}

		private static bool GetBoolValue(string text)
		{
			Match match = s_propertyRegex.Match(text);
			while ( match.Success )
			{
				string value = match.Groups[1].Value.Trim();
				if ( !string.IsNullOrEmpty(value) )
					return value.Equals(value: "YES", StringComparison.OrdinalIgnoreCase);

				match = match.NextMatch();
			}

			return false;
		}
	}
}
