using Newtonsoft.Json;
using Server.Configs;

namespace Server.Bonuses
{
    internal sealed class BonusMapper : IBonusMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("operator")]
        public BonusOperatorType OperatorType { get; init; }

        [JsonProperty("work_modes")]
        public string WorkModeParameters { get; init; } = string.Empty;

        [JsonProperty("bonus_type")]
        public BonusType BonusType { get; init; }

        [JsonProperty("bonus_value")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] BonusValues { get; init; } = Array.Empty<float>();

        [JsonProperty("is_displayed")]
        [OptionalColumn("старые снапшоты не содержат настройки отображения; иконка скрыта")]
        public bool IsDisplayed { get; init; }

        [JsonProperty("icon_name")]
        [OptionalColumn("старые снапшоты не содержат имени иконки; обязательно при is_displayed = true")]
        public string IconName { get; init; } = string.Empty;

        public float BonusValue => BonusValues.Length == 0 ? 0f : BonusValues[0];
    }
}
