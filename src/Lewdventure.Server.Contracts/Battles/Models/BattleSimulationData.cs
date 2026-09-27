namespace Server.Battles
{
    public sealed class BattleSimulationData : IBattleSimulationData
    {
        public ITeamSnapshot TeamA { get; set; } = null!;

        public ITeamSnapshot TeamB { get; set; } = null!;

        public int StoryLevelId { get; set; }

        public int StageId { get; set; }
    }
}
