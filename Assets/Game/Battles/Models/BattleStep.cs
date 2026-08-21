namespace Server.Battles
{
    internal sealed class BattleStep
    {
        public int Index { get; set; }

        public int Turn { get; set; }

        public BattlePhaseType Phase { get; set; }

        public int ActorId { get; set; }

        public int ActorSlotIndex { get; set; } = -1;

        public int TargetId { get; set; } = -1;

        public int TargetSlotIndex { get; set; } = -1;

        public List<BattleCommand> Commands { get; set; } = new();
    }
}
