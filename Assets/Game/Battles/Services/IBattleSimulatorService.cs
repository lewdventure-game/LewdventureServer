namespace Server.Battles
{
    internal interface IBattleSimulatorService
    {
        public IBattleScriptResponse Simulate(IBattleSimulationData data);

        public IBattleScriptResponse Replay(IBattleReplayData data);
    }
}

