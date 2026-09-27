namespace Server.Battles
{
    internal sealed class BattleTeamQuery : IBattleTeamQuery
    {
        public int FindTargetIndex(ITeamSimulationState defender)
        {
            var mainUnits = defender.MainUnits;
            var selectedIndex = -1;
            var selectedSlot = int.MaxValue;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];

                if (unit.IsAlive() == false)
                    continue;

                if (selectedSlot <= unit.SlotIndex)
                    continue;

                selectedSlot = unit.SlotIndex;
                selectedIndex = i;
            }

            return selectedIndex;
        }

        public bool HasAliveMainUnits(ITeamSimulationState state)
        {
            var mainUnits = state.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                if (mainUnits[i].IsAlive())
                    return true;
            }

            return false;
        }
    }
}
