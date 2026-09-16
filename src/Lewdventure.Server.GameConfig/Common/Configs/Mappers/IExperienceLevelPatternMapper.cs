namespace Server.Configs
{
    internal interface IExperienceLevelPatternMapper : IConfigMapper
    {
        public int Id { get; }

        public int ExperienceLevel { get; }

        public int ExperienceForNextLevel { get; }

        public int[] PerkPresetIds { get; }

        public int[] PerkPresetWeights { get; }
    }
}
