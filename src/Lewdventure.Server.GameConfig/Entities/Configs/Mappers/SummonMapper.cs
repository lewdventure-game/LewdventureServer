using Newtonsoft.Json;
using Server.Common;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class SummonMapper : ISummonMapper
    {
        private string _artName = string.Empty;

        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("art_name")]
        [OptionalColumn("без колонки берётся skin_name")]
        private string ArtNameRaw
        {
            set { _artName = value ?? string.Empty; }
        }

        public string ArtName => string.IsNullOrEmpty(_artName) ? SkinName : _artName;

        [JsonProperty("skin_name")]
        [OptionalColumn("визуал боя: id префаба в Unity")]
        public string SkinName { get; init; } = string.Empty;

        [JsonProperty("rarity")]
        public RarityType Rarity { get; init; }

        [JsonProperty("dmg_on_lvls")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] DamageOnLevels { get; init; } = Array.Empty<float>();

        [JsonProperty("attack_cooldown")]
        public int AttackCooldown { get; init; }

        [JsonProperty("is_melee")]
        public bool IsMelee { get; init; }

        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] SkillIds { get; init; } = Array.Empty<string>();

        [JsonProperty("mastery_for_skills")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] MasteryForSkills { get; init; } = Array.Empty<int>();

        [JsonProperty("skill_upgrade_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillUpgradeIds { get; init; } = Array.Empty<int>();

        [JsonProperty("mastery_id")]
        public int MasteryId { get; init; }

        [JsonProperty("level_pattern_id")]
        public int LevelPatternId { get; init; }
    }
}
