// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
    public class DirectoryInfoConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType ) => objectType == typeof( DirectoryInfo );

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer
        )
        {
            string? path = reader.Value as string;
            return new DirectoryInfo( path ?? throw new NullReferenceException() );
        }

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if ( value is null )
                return;

            writer.WriteValue( ( (DirectoryInfo)value ).FullName );
        }
    }
}
