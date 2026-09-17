namespace Server.GameConfigs
{
    internal sealed class ConfigDomainNames
    {
        public const string Constants = "Constants";
        public const string Characters = "Characters";
        public const string Bonuses = "Bonuses";
        public const string Statuses = "Statuses";
        public const string Summons = "Summons";
        public const string SummonLevels = "Summon_levels";
        public const string Mastery = "Mastery";
        public const string Enemies = "Enemies";
        public const string Equipments = "Equipments";
        public const string StoryLevels = "Story_levels";
        public const string StoryStages = "Story_stages";
        public const string StoryEvents = "Story_events";
        public const string ExpLevelsPatterns = "Exp_levels_patterns";
        public const string Perks = "Perks";
        public const string PerkGroups = "Perk_groups";

        private readonly string[] _ordered =
        {
            Constants,
            Characters,
            Bonuses,
            Statuses,
            Summons,
            SummonLevels,
            Mastery,
            Enemies,
            Equipments,
            StoryLevels,
            StoryStages,
            StoryEvents,
            ExpLevelsPatterns,
            Perks,
            PerkGroups,
        };

        public IReadOnlyList<string> Ordered => _ordered;
    }
}
