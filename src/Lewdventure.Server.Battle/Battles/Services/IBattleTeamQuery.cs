namespace Server.Battles
{
    internal interface IBattleTeamQuery
    {
        public int FindTargetIndex(ITeamSimulationState defender);

        public bool HasAliveMainUnits(ITeamSimulationState state);
    }
}
