using Server.Services;

namespace Server.Battles
{
    internal sealed class PerkTurnPhase : IBattleTurnPhase
    {
        private readonly IBattlePerkSimulator _battlePerkSimulator;

        public PerkTurnPhase(IBattlePerkSimulator battlePerkSimulator)
        {
            _battlePerkSimulator = battlePerkSimulator;
        }

        public string Name => "perks";

        public bool StopsSideTurnWhenAttackerDead => false;

        public void Execute(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _battlePerkSimulator.Simulate(steps, attacker, defender, currentTurn, seededRandomService, turnState);
        }
    }
}
