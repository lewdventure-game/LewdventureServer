namespace Server.Battles
{
    internal sealed class TeamSimulationState : ITeamSimulationState
    {
        private readonly IReadOnlyList<IUnitState> _mainUnits;
        private readonly IReadOnlyList<IUnitState> _summons;
        private readonly BattleSide _battleSide;

        public IReadOnlyList<IUnitState> MainUnits => _mainUnits;

        public IReadOnlyList<IUnitState> Summons => _summons;

        public BattleSide BattleSide => _battleSide;

        public TeamSimulationState(
            IReadOnlyList<IUnitState> mainUnits,
            IReadOnlyList<IUnitState> summons,
            BattleSide battleSide)
        {
            _mainUnits = mainUnits;
            _summons = summons;
            _battleSide = battleSide;
        }
    }
}
