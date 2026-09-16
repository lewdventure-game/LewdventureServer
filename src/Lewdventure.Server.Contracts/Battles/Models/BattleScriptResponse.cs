namespace Server.Battles
{
    internal sealed class BattleScriptResponse : IBattleScriptResponse
    {
        public int ProtocolVersion { get; set; } = 1;

        public ulong Seed { get; set; }

        public OutcomeType OutcomeType { get; set; }

        public List<BattleStep> Steps { get; set; } = new();
    }
}
