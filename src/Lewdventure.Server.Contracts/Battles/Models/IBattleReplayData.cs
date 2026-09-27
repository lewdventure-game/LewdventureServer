namespace Server.Battles
{
    public interface IBattleReplayData : IBattleSimulationData
    {
        public ulong Seed { get; set; }
    }
}
