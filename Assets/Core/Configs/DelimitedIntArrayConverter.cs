using System.Globalization;
using Newtonsoft.Json;

namespace Server.Configs
{
    /// <summary>
    /// Конвертер для парсинга строки с разделителями (например, "1;2" или "[1;2]") в массив int.
    /// Также умеет читать стандартные JSON-массивы [1, 2] и скалярные числа для обратной совместимости.
    /// </summary>
    internal sealed class DelimitedIntArrayConverter : JsonConverter
    {
        private readonly char _delimiter;

        public DelimitedIntArrayConverter(char delimiter = ';')
        {
            _delimiter = delimiter;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<int>();

            if (reader.TokenType == JsonToken.Integer)
            {
                var number = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);

                return new[] { number };
            }

            if (reader.TokenType == JsonToken.Float)
            {
                var number = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);

                return new[] { number };
            }

            if (reader.TokenType == JsonToken.String)
            {
                var value = (string)reader.Value;

                if (string.IsNullOrWhiteSpace(value))
                    return Array.Empty<int>();

                value = value.Trim('[', ']');
                var parts = value.Split(_delimiter);
                var numbers = new List<int>(parts.Length);

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();

                    if (string.IsNullOrWhiteSpace(part))
                        continue;

                    numbers.Add(int.Parse(part, CultureInfo.InvariantCulture));
                }

                return numbers.ToArray();
            }

            if (reader.TokenType == JsonToken.StartArray)
                return serializer.Deserialize<int[]>(reader);

            throw new JsonSerializationException($"Неожиданный токен при парсинге int[]: {reader.TokenType}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
