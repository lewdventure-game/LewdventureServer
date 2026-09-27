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
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<TEnum>();

            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value is string rawValue
                    ? ParseDelimitedString(rawValue)
                    : [];
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var values = serializer.Deserialize<TEnum[]>(reader);

                return values ?? [];
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing {typeof(TEnum).Name}[]");
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        private TEnum[] ParseDelimitedString(string rawValue)
        {
            var value = rawValue.Trim('[', ']');

            if (string.IsNullOrWhiteSpace(value))
                return [];

            var parts = value.Split(_delimiter);
            var parsedValues = new List<TEnum>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (string.IsNullOrWhiteSpace(part))
                    continue;

                parsedValues.Add(ParseEnum(part));
            }

            return parsedValues.ToArray();
        }

        private TEnum ParseEnum(string value)
        {
            var fields = typeof(TEnum).GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var customAttributes = field.GetCustomAttributes(typeof(EnumMemberAttribute), false);

                if (customAttributes.Length == 0)
                    continue;

                if (customAttributes[0] is not EnumMemberAttribute attribute)
                    continue;

                if (string.Equals(attribute.Value, value, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                if (field.GetValue(null) is TEnum parsed)
                    return parsed;
            }

            if (Enum.TryParse<TEnum>(value, true, out var result))
                return result;

            return Enum.TryParse(ToPascalCase(value), true, out result) ? result : default;
        }

        private string ToPascalCase(string value)
        {
            var parts = value.Split('_');
            var builder = new System.Text.StringBuilder(value.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (part.Length == 0)
                    continue;

                builder.Append(char.ToUpperInvariant(part[0]));
                builder.Append(part.Substring(1).ToLowerInvariant());
            }

            return builder.ToString();
        }

    }
}
