using Server.Services;

namespace Server.Battles
{
    internal interface IBattleTurnPhase
    {
        public string Name { get; }

        public bool StopsSideTurnWhenAttackerDead { get; }

        public void Execute(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState);
    }
}
