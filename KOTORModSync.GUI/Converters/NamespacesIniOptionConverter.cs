// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	public class NamespacesIniOptionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				if ( !(value is Instruction dataContextInstruction) )
					return null;

				Component parentComponent = dataContextInstruction.GetParentComponent();
				if ( parentComponent is null )
					return null;

				return (from archivePath in GetAllArchivesFromInstructions(parentComponent)
					where !string.IsNullOrEmpty(archivePath)
					let result = IniHelper.ReadNamespacesIniFromArchive(archivePath)
					where result != null && result.Any()
					let optionNames = result
						.Where(section => section.Value?.TryGetValue(key: "Name", out _) ?? false)
						.Select(section => section.Value["Name"]).ToList()
					where optionNames.Any()
					select optionNames).FirstOrDefault();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return null;
			}
		}

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

		[NotNull]
		public List<string> GetAllArchivesFromInstructions([NotNull] Component parentComponent)
		{
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof( parentComponent ));

			var allArchives = new List<string>();

			var instructions = parentComponent.Instructions.ToList();
			foreach ( Option thisOption in parentComponent.Options )
			{
				if ( thisOption is null )
					continue;

				instructions.AddRange(thisOption.Instructions);
			}

			foreach ( Instruction instruction in instructions )
			{
				if ( instruction.Action != Instruction.ActionType.Extract )
					continue;

				List<string> realPaths = PathHelper.EnumerateFilesWithWildcards(
					instruction.Source.ConvertAll(Utility.ReplaceCustomVariables),
					includeSubFolders: false
				);
				if ( !realPaths?.IsNullOrEmptyCollection() ?? false )
				{
					allArchives.AddRange(realPaths.Where(File.Exists));
				}
			}

			return allArchives;
		}
	}
}
