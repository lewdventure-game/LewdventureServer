using Server.Services;

namespace Server.Battles
{
    internal interface IBattleStatusSimulator
    {
        public void EmitInitialStatuses(IUnitState unitState, List<BattleStep> steps, int currentTurn);

        public void SimulateSide(
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService);
    }
}
