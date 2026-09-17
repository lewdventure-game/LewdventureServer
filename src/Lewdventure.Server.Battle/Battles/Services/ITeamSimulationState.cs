namespace Server.Battles
{
    internal interface ITeamSimulationState
    {
        public IReadOnlyList<IUnitState> MainUnits { get; }

        public IReadOnlyList<IUnitState> Summons { get; }

        public BattleSide BattleSide { get; }
    }
}
