using Server.Services;

namespace Server.Battles
{
    internal interface IBattleAttackService
    {
        public void SimulateMainUnits(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);
    }
}
