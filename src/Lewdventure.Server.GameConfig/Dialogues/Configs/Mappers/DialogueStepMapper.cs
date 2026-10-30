using Newtonsoft.Json;
using Server.Configs;

namespace Server.Dialogues
{
    internal sealed class DialogueStepMapper : IDialogueStepMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("step")]
        public int Step { get; init; }

        [JsonProperty("next_step")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] NextSteps { get; init; } = Array.Empty<int>();

        [JsonProperty("is_fade_on_start")]
        public bool IsFadeOnStart { get; init; }

        [JsonProperty("dialogue_characters_visible")]
        [JsonConverter(typeof(DialogueCharacterArrayConverter))]
        public DialogueCharacter[] Characters { get; init; } = Array.Empty<DialogueCharacter>();

        [JsonProperty("current_character_loc_name")]
        public string CurrentCharacterLocName { get; init; } = string.Empty;

        [JsonProperty("backgrounds")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] Backgrounds { get; init; } = Array.Empty<string>();

        [JsonProperty("music")]
        public string Music { get; init; } = string.Empty;

        [JsonProperty("sounds")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';', true)]
        public string[] Sounds { get; init; } = Array.Empty<string>();

        [JsonProperty("localization_keys")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] LocalizationKeys { get; init; } = Array.Empty<string>();

        [JsonProperty("text_speed")]
        public float TextSpeed { get; init; }
    }
}
