using Newtonsoft.Json;

namespace Server.Battles
{
    internal sealed class EquipmentSnapshotJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IEquipmentSnapshot);
        }

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            return reader.TokenType != JsonToken.Null
                ? (object?)serializer.Deserialize<EquipmentSnapshot>(reader)
                : null;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
