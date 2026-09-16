using Server.Services;

namespace Server.Battles
{
    internal interface IBattleSkillSimulator
    {
        public void SimulateUnitSkills(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);

        public void SimulateSummonSkills(
            List<BattleStep> steps,
            IUnitState summon,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);

        public void ApplyEnergyGain(
            List<BattleStep> steps,
            IUnitState actor,
            int currentTurn);

        public void TryCastEnergySkill(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService);
    }
}
