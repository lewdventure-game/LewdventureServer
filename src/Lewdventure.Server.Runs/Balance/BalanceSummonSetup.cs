namespace Server.Balance
{
    internal sealed class BalanceSummonSetup
    {
        public int Id { get; set; }

        public int Level { get; set; } = 1;

        public int MasteryLevel { get; set; } = 1;

        public List<int> SkillLevels { get; set; } = new();
    }
}
