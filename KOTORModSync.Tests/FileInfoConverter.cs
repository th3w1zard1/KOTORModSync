using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
    public class FileInfoConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType ) => objectType == typeof( FileInfo );

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer
        ) =>
            reader.Value is not string filePath
                ? default
                : (object)new FileInfo( filePath );

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if ( value is not FileInfo fileInfo )
                return;

            writer.WriteValue( fileInfo.FullName );
        }
    }
}
