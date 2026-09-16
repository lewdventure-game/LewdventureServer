
namespace Server.Battles
{
    internal interface IBattleScriptResponse
    {
        public int ProtocolVersion { get; set; }

        public ulong Seed { get; set; }

        public OutcomeType OutcomeType { get; set; }

        public List<BattleStep> Steps { get; set; }
    }
}
