namespace Server.Battles
{
    internal sealed class BattleTurnState
    {
        private readonly List<int> _abortedUnitIds = new();
        private readonly List<int> _abortedUnitSlots = new();

        private IUnitState? _actingUnit;

        public IUnitState? ActingUnit => _actingUnit;

        public int ActingUnitId => _actingUnit == null ? -1 : _actingUnit.Id;

        public void BeginSideTurn()
        {
            _abortedUnitIds.Clear();
            _abortedUnitSlots.Clear();
            _actingUnit = null;
        }

        public void SetActingUnit(IUnitState unit)
        {
            _actingUnit = unit;
        }

        public bool ShouldSkipRemainingActions(IUnitState unit)
        {
            for (int i = 0; i < _abortedUnitIds.Count; i++)
            {
                if (_abortedUnitIds[i] != unit.Id)
                    continue;

                if (_abortedUnitSlots[i] != unit.SlotIndex)
                    continue;

                return true;
            }

            return false;
        }

        public bool AbortUnit(IUnitState unit)
        {
            if (ShouldSkipRemainingActions(unit))
                return false;

            _abortedUnitIds.Add(unit.Id);
            _abortedUnitSlots.Add(unit.SlotIndex);

            return true;
        }
    }
}
