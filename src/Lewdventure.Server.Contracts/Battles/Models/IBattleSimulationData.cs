namespace Server.Battles
{
    internal interface IBattleSimulationData
    {
        public int StoryLevelId { get; set; }

        public int StageId { get; set; }

        public ITeamSnapshot TeamA { get; set; }

        public ITeamSnapshot TeamB { get; set; }
    }
}
