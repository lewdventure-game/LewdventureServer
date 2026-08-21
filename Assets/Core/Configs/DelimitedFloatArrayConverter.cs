using System.Globalization;
using Newtonsoft.Json;

namespace Server.Configs
{
    internal sealed class DelimitedFloatArrayConverter : JsonConverter
    {
        private readonly char _delimiter;

        public DelimitedFloatArrayConverter(char delimiter = ';')
        {
            _delimiter = delimiter;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(float[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<float>();

            if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
            {
                var number = Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture);

                return new[] { number };
            }

            if (reader.TokenType == JsonToken.String)
            {
                var value = (string)reader.Value;

                if (string.IsNullOrWhiteSpace(value))
                    return Array.Empty<float>();

                value = value.Trim('[', ']');
                var parts = value.Split(_delimiter);
                var numbers = new List<float>(parts.Length);

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();

                    if (string.IsNullOrWhiteSpace(part))
                        continue;

                    numbers.Add(float.Parse(part, CultureInfo.InvariantCulture));
                }

                return numbers.ToArray();
            }

            if (reader.TokenType == JsonToken.StartArray)
                return serializer.Deserialize<float[]>(reader);

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing float[]");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
