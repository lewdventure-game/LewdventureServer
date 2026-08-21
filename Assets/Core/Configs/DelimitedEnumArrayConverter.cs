using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Server.Configs
{
    internal sealed class DelimitedEnumArrayConverter<TEnum> : JsonConverter
        where TEnum : struct, Enum
    {
        private readonly char _delimiter;

        public DelimitedEnumArrayConverter(char delimiter = ';')
        {
            _delimiter = delimiter;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TEnum[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<TEnum>();

            if (reader.TokenType == JsonToken.String)
            {
                var value = ((string)reader.Value).Trim('[', ']');

                if (string.IsNullOrWhiteSpace(value))
                    return Array.Empty<TEnum>();

                return value
                    .Split(_delimiter)
                    .Where(stringValue => string.IsNullOrWhiteSpace(stringValue) == false)
                    .Select(stringValue => ParseEnum(stringValue.Trim()))
                    .ToArray();
            }

            if (reader.TokenType == JsonToken.StartArray)
                return serializer.Deserialize<TEnum[]>(reader);

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing {typeof(TEnum).Name}[]");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        private static TEnum ParseEnum(string value)
        {
            // 1. Сначала ищем по [EnumMember(Value = "...")]
            foreach (var field in typeof(TEnum).GetFields())
            {
                var attribute = field.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                    .FirstOrDefault() as EnumMemberAttribute;

                if (attribute != null && string.Equals(attribute.Value, value, StringComparison.OrdinalIgnoreCase))
                    return (TEnum)field.GetValue(null);
            }

            // 2. Пытаемся стандартный парсинг (с учётом регистра)
            if (Enum.TryParse<TEnum>(value, true, out var result))
                return result;

            // 3. Пытаемся с SnakeCase -> PascalCase
            var pascalCase = string.Concat(value.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));

            if (Enum.TryParse(pascalCase, true, out result))
                return result;

            return default;
        }
    }
}
