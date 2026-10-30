using System.Globalization;
using Newtonsoft.Json;
using Server.Configs;

namespace Server.Dialogues
{
    internal sealed class DialogueCharacterArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DialogueCharacter[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<DialogueCharacter>();

            if (reader.TokenType != JsonToken.String || reader.Value is not string value || string.IsNullOrWhiteSpace(value))
                return Array.Empty<DialogueCharacter>();

            var parts = value.Split(SeparatorFormat.PairSeparator);
            var characters = new List<DialogueCharacter>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var separator = part.LastIndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator <= 0 || separator == part.Length - 1)
                    throw new JsonSerializationException($"спрайт «{part}» без процента, нужен формат имя:процент");

                var spriteName = part.Substring(0, separator).Trim();
                var percentText = part.Substring(separator + 1).Trim().Replace(',', '.');

                if (float.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) == false)
                    throw new JsonSerializationException($"у спрайта «{spriteName}» процент «{percentText}» не число");

                characters.Add(new DialogueCharacter(spriteName, percent));
            }

            return characters.ToArray();
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not DialogueCharacter[] characters)
            {
                writer.WriteNull();

                return;
            }

            var parts = new string[characters.Length];

            for (int i = 0; i < characters.Length; i++)
                parts[i] = characters[i].SpriteName + SeparatorFormat.KeyValueSeparator + characters[i].ScreenPercent.ToString(CultureInfo.InvariantCulture);

            writer.WriteValue(string.Join(SeparatorFormat.PairSeparator, parts));
        }
    }
}
