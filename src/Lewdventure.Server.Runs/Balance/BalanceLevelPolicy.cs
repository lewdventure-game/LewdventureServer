namespace Server.Balance
{
    internal sealed class BalanceLevelPolicy
    {
        public BalancePerkPolicy PerkPolicy { get; set; } = BalancePerkPolicy.First;

        public List<int> PerkPriority { get; set; } = new();

        public BalanceForkPolicy ForkPolicy { get; set; } = BalanceForkPolicy.FirstButton;
    }
}
