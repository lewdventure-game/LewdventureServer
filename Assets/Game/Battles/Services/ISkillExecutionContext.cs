using Server.Services;

namespace Server.Battles
{
    internal interface ISkillExecutionContext
    {
        public List<BattleStep> Steps { get; }

        public IUnitState Actor { get; }

        public IUnitState Target { get; }

        public ITeamSimulationState Attacker { get; }

        public ITeamSimulationState Defender { get; }

        public int CurrentTurn { get; }

        public BattlePhaseType Phase { get; }

        public bool AllowCritical { get; }

        public float Cooldown { get; }

        public ISeededRandomService SeededRandomService { get; }

        public IBattleCommandFactory BattleCommandFactory { get; }

        public IBattleBonusService BattleBonusService { get; }

        public IBattleRewardService BattleRewardService { get; }

        public IBattleScriptBuilder BattleScriptBuilder { get; }

        public ILogger Logger { get; }

        public bool TryDealStrike(List<BattleCommand> commands, float damageMultiplier, out float dealtDamage, out bool isCritical);

        public void Heal(IUnitState unit, float amount, List<BattleCommand> commands);

        public int FindAllyMainIndex();

        public void EmitDeath(IUnitState unit);

        public void AddStep(List<BattleCommand> commands, IUnitState target);
    }
}
