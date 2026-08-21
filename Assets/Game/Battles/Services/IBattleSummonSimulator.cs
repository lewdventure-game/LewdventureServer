using Server.Services;

namespace Server.Battles
{
    internal interface IBattleSummonSimulator
    {
        public void EmitInitialSpawns(List<BattleStep> steps, ITeamSimulationState team, int currentTurn);

        public void Simulate(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);
    }
}
