using Server.Services;

namespace Server.Battles
{
    internal interface IPerkExecutionContext
    {
        public List<BattleStep> Steps { get; }

        public IUnitState Owner { get; }

        public ITeamSimulationState Attacker { get; }

        public ITeamSimulationState Defender { get; }

        public int CurrentTurn { get; }

        public ISeededRandomService SeededRandomService { get; }

        public IBattleBonusService BattleBonusService { get; }

        public IBattleCommandFactory BattleCommandFactory { get; }

        public IBattleScriptBuilder BattleScriptBuilder { get; }

        public IBattleRewardService BattleRewardService { get; }

        public IConfigDistributor ConfigDistributor { get; }

        public ILogger Logger { get; }

        public int FindDefenderTargetIndex();

        public void NotifyAnyDamage();

        public void EmitDeath(IUnitState unit);
    }
}
