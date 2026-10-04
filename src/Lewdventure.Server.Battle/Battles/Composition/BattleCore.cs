using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleCore : IBattleCore
    {
        private readonly BattleComposition _battleComposition;

        public BattleCore(IConfigDistributor configDistributor, ICoreLog coreLog, IBattleRollRecorder? battleRollRecorder)
        {
            _battleComposition = battleRollRecorder == null
                ? new BattleComposition(configDistributor, coreLog)
                : new BattleComposition(configDistributor, coreLog, new RecordingSeededRandomFactory(battleRollRecorder));
        }

        public IBattleScriptResponse Simulate(IBattleSimulationData data)
        {
            return _battleComposition.BattleSimulatorService.Simulate(data);
        }

        public IBattleScriptResponse Replay(IBattleReplayData data)
        {
            return _battleComposition.BattleSimulatorService.Replay(data);
        }

        public bool TryValidate(IBattleSimulationData data, out string errorMessage)
        {
            return _battleComposition.BattleSimulationValidator.TryValidate(data, out errorMessage);
        }

        public string ComputeDigest(IBattleScriptResponse script)
        {
            return _battleComposition.BattleScriptDigest.Compute(script);
        }

        public ICharacteristicState BuildCharacteristics(IUnitSnapshot unitSnapshot, BattleSide battleSide, bool isSummon, int storyLevelId, int stageId)
        {
            var unitState = _battleComposition.UnitStateBuilder.Build(unitSnapshot, battleSide, isSummon, storyLevelId, stageId);

            return unitState.CharacteristicState;
        }
    }
}
