namespace Server.Battles
{
    public interface IBattleCore
    {
        public IBattleScriptResponse Simulate(IBattleSimulationData data);

        public IBattleScriptResponse Replay(IBattleReplayData data);

        public bool TryValidate(IBattleSimulationData data, out string errorMessage);

        public string ComputeDigest(IBattleScriptResponse script);

        public ICharacteristicState BuildCharacteristics(IUnitSnapshot unitSnapshot, BattleSide battleSide, bool isSummon, int storyLevelId, int stageId);
    }
}
