namespace Server.Battles
{
    internal sealed class SkillTargetResolver : ISkillTargetResolver
    {
        private readonly ICoreLog _coreLog;

        public SkillTargetResolver(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public void Resolve(
            SkillTargetType targetType,
            IUnitState owner,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<IUnitState> targets)
        {
            targets.Clear();

            if (targetType == SkillTargetType.Ally)
            {
                AddAlly(owner, ownerTeam, targets);

                return;
            }

            if (targetType == SkillTargetType.AllEnemies)
            {
                AddAliveMains(opponentTeam, targets);

                return;
            }

            var preferredSlot = targetType == SkillTargetType.EnemySecondSlot ? 1 : 0;

            if (TryAddEnemyInSlot(opponentTeam, preferredSlot, targets))
                return;

            var fallbackSlot = preferredSlot == 0 ? 1 : 0;

            if (TryAddEnemyInSlot(opponentTeam, fallbackSlot, targets))
                return;

            _coreLog.Debug($"[Story][Battle]: Skill target missing, targetType = {targetType}, ownerId = {owner.Id}");
        }

        private void AddAlly(IUnitState owner, ITeamSimulationState ownerTeam, List<IUnitState> targets)
        {
            if (owner.IsAlive())
            {
                targets.Add(owner);

                return;
            }

            AddAliveMains(ownerTeam, targets);
        }

        private void AddAliveMains(ITeamSimulationState team, List<IUnitState> targets)
        {
            var mainUnits = team.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                if (mainUnits[i].IsAlive() == false)
                    continue;

                targets.Add(mainUnits[i]);
            }
        }

        private bool TryAddEnemyInSlot(ITeamSimulationState opponentTeam, int slotIndex, List<IUnitState> targets)
        {
            var mainUnits = opponentTeam.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];

                if (unit.SlotIndex != slotIndex || unit.IsAlive() == false)
                    continue;

                targets.Add(unit);

                return true;
            }

            return false;
        }
    }
}
