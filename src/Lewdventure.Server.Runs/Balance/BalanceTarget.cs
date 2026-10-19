namespace Server.Balance
{
    internal sealed class BalanceTarget
    {
        public int StoryLevelId { get; set; } = 1;

        public int StageId { get; set; }

        public int EventId { get; set; }

        public List<int> EnemyIds { get; set; } = new();
    }
}
