namespace Server.Battles
{
    internal interface IBattleReplayData : IBattleSimulationData
    {
        public ulong Seed { get; set; }
    }
}
