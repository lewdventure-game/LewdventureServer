namespace Server.Admin.Backend.Models
{
    public sealed class BalanceTargetModel
    {
        public int StoryLevelId { get; set; } = 1;

        public int StageId { get; set; }

        public int EventId { get; set; }

        public List<int> EnemyIds { get; set; } = new();
    }
}
