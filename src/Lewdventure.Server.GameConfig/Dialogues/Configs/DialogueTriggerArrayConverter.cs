using System.Globalization;
using Newtonsoft.Json;
using Server.Configs;

namespace Server.Dialogues
{
    internal sealed class DialogueTriggerArrayConverter : JsonConverter
    {
        private const string OnStart = "on_start";
        private const string OnStageEntered = "on_stage_entered";
        private const string OnWin = "on_win";
        private const string OnLoss = "on_loss";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DialogueTrigger[]);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Array.Empty<DialogueTrigger>();

            if (reader.TokenType != JsonToken.String || reader.Value is not string value || string.IsNullOrWhiteSpace(value))
                return Array.Empty<DialogueTrigger>();

            var parts = value.Split(SeparatorFormat.PairSeparator);
            var triggers = new List<DialogueTrigger>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                triggers.Add(Parse(part));
            }

            return triggers.ToArray();
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not DialogueTrigger[] triggers)
            {
                writer.WriteNull();

                return;
            }

            var parts = new string[triggers.Length];

            for (int i = 0; i < triggers.Length; i++)
                parts[i] = triggers[i].Source;

            writer.WriteValue(string.Join(SeparatorFormat.PairSeparator, parts));
        }

        private DialogueTrigger Parse(string source)
        {
            var normalized = source.ToLowerInvariant();

            if (string.Equals(normalized, OnStart, StringComparison.Ordinal))
                return new DialogueTrigger(DialogueTriggerType.OnStart, 0, source);

            if (string.Equals(normalized, OnWin, StringComparison.Ordinal))
                return new DialogueTrigger(DialogueTriggerType.OnWin, 0, source);

            if (string.Equals(normalized, OnLoss, StringComparison.Ordinal))
                return new DialogueTrigger(DialogueTriggerType.OnLoss, 0, source);

            var separator = normalized.IndexOf(SeparatorFormat.KeyValueSeparator);

            if (separator < 0 || string.Equals(normalized.Substring(0, separator).Trim(), OnStageEntered, StringComparison.Ordinal) == false)
                return new DialogueTrigger(DialogueTriggerType.Unknown, 0, source);

            var stageText = normalized.Substring(separator + 1).Trim();

            if (int.TryParse(stageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stageNumber) == false)
                return new DialogueTrigger(DialogueTriggerType.Unknown, 0, source);

            return new DialogueTrigger(DialogueTriggerType.OnStageEntered, stageNumber, source);
        }
    }
}
