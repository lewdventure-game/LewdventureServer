using Server.Services;

namespace Server.Battles
{
    internal sealed class StatusTurnPhase : IBattleTurnPhase
    {
        private readonly IBattleStatusSimulator _battleStatusSimulator;

        public StatusTurnPhase(IBattleStatusSimulator battleStatusSimulator)
        {
            _battleStatusSimulator = battleStatusSimulator;
        }

        public string Name => "statuses";

        public bool StopsSideTurnWhenAttackerDead => true;

        public void Execute(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _battleStatusSimulator.SimulateSide(attacker, defender, steps, currentTurn, seededRandomService, turnState);
        }
    }
}
