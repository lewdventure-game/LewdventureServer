using Server.Services;

namespace Server.Battles
{
    internal sealed class SummonTurnPhase : IBattleTurnPhase
    {
        private readonly IBattleSummonSimulator _battleSummonSimulator;

        public SummonTurnPhase(IBattleSummonSimulator battleSummonSimulator)
        {
            _battleSummonSimulator = battleSummonSimulator;
        }

        public string Name => "summons";

        public bool StopsSideTurnWhenAttackerDead => false;

        public void Execute(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _battleSummonSimulator.Simulate(steps, attacker, defender, currentTurn, seededRandomService, turnState);
        }
    }
}
