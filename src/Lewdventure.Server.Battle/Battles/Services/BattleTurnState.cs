namespace Server.Battles
{
    internal sealed class BattleTurnState
    {
        private IUnitState? _actingUnit;
        private bool _isSideTurnAborted;

        public IUnitState? ActingUnit => _actingUnit;

        public int ActingUnitId => _actingUnit == null ? -1 : _actingUnit.Id;

        public bool IsSideTurnAborted => _isSideTurnAborted;

        public void BeginSideTurn()
        {
            _actingUnit = null;
            _isSideTurnAborted = false;
        }

        public void SetActingUnit(IUnitState unit)
        {
            _actingUnit = unit;
        }

        public bool ShouldSkipRemainingActions(IUnitState unit)
        {
            return _isSideTurnAborted;
        }

        public void AbortSideTurn()
        {
            _isSideTurnAborted = true;
        }
    }
}
