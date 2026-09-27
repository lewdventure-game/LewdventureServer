using Server.Services;

namespace Server.Battles
{
    internal sealed class MainUnitTurnPhase : IBattleTurnPhase
    {
        private readonly IBattleAttackService _battleAttackService;

        public MainUnitTurnPhase(IBattleAttackService battleAttackService)
        {
            _battleAttackService = battleAttackService;
        }

        public string Name => "main-units";

        public bool StopsSideTurnWhenAttackerDead => false;

        public void Execute(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _battleAttackService.SimulateMainUnits(steps, attacker, defender, currentTurn, seededRandomService, turnState);
        }
    }
}
