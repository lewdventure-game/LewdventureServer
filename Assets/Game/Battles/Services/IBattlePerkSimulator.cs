using Server.Services;

namespace Server.Battles
{
    internal interface IBattlePerkSimulator
    {
        public bool ShouldAbortRemainingTurn { get; }

        public void BeginBattleTurn();

        public void BeginSideTurn(BattleSide actingSide);

        public void Simulate(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);

        public void NotifyAction(
            BattlePerkActionType actionType,
            IUnitState actor,
            ITeamSimulationState actorTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService);

        public void NotifyAnyDamage(
            IUnitState damageDealer,
            ITeamSimulationState dealerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService);

        public bool TryResurrectOnDeath(IUnitState unit, List<BattleStep> steps, int currentTurn);
    }
}
