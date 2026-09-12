using Newtonsoft.Json;

namespace Server.Configs
{
    internal sealed class DelimitedStringArrayConverter : JsonConverter
    {
        private readonly char _delimiter;

        public DelimitedStringArrayConverter(char delimiter = ';')
        {
            _delimiter = delimiter;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<string>();

            if (reader.TokenType == JsonToken.String)
            {
                if (reader.Value is string value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return Array.Empty<string>();

                    value = value.Trim('[', ']');
                    var parts = value.Split(_delimiter);
                    var values = new List<string>(parts.Length);

                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i].Trim().Trim('"');

                        if (string.IsNullOrWhiteSpace(part))
                            continue;

                        values.Add(part);
                    }

                    return values.ToArray();
                }

                return Array.Empty<string>();
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var values = serializer.Deserialize<string[]>(reader);

                return values ?? Array.Empty<string>();
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing string[]");
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
