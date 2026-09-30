using Server.Perks;

namespace Server.Battles
{
    internal sealed class ResurrectionPerk : BasePerk
    {
        private readonly float _healthRatio;
        private readonly int _resurrectionsCount;
        private int _usedResurrections;
        private readonly ICoreLog _coreLog;

        internal ResurrectionPerk(
            IPerkMapper mapper,
            float healthRatio,
            int resurrectionsCount,
            ICoreLog coreLog)
            : base(mapper)
        {
            _healthRatio = healthRatio;
            _resurrectionsCount = resurrectionsCount;
            _coreLog = coreLog;
        }

        public override int UsedCount => _usedResurrections;

        public override int RemainingUses => ResolveRemaining();

        public override void RestoreUsage(int usedCount)
        {
            if (usedCount <= 0)
                return;

            _usedResurrections = usedCount;

            if (_resurrectionsCount < _usedResurrections)
                _usedResurrections = _resurrectionsCount;

            _coreLog.Debug($"[Story][Battle]: Perk resurrection usage restored, perkId = {Id}, used = {_usedResurrections}, remaining = {ResolveRemaining()}");
        }

        public override bool CanTrigger(int currentTurn)
        {
            return 0 < ResolveRemaining();
        }

        public override void Trigger(IPerkExecutionContext context)
        {
            TryResurrectOnDeath(
                context.Owner,
                context.Steps,
                context.CurrentTurn,
                context.BattleCommandFactory,
                context.BattleScriptBuilder);
        }

        public override bool TryResurrectOnDeath(
            IUnitState owner,
            List<BattleStep> steps,
            int currentTurn,
            IBattleCommandFactory battleCommandFactory,
            IBattleScriptBuilder battleScriptBuilder)
        {
            if (ResolveRemaining() <= 0)
                return false;

            if (0 < owner.CharacteristicState.Health)
            {
                _coreLog.Debug($"[Story][Battle]: Perk resurrection skipped alive, perkId = {Id}, ownerId = {owner.Id}");

                return false;
            }

            var characteristics = owner.CharacteristicState;
            var healRatio = _healthRatio;

            if (healRatio < 0f)
                healRatio = 0f;

            if (1f < healRatio)
                healRatio = 1f;

            var healthBefore = characteristics.Health;
            var healthAfter = characteristics.MaxHealth * healRatio;

            if (healthAfter < 1f)
                healthAfter = 1f;

            var healDelta = healthAfter - healthBefore;

            if (healDelta < 0f)
                healDelta = 0f;

            characteristics.Health = healthAfter;
            _usedResurrections += 1;

            var commands = new List<BattleCommand>
            {
                battleCommandFactory.TriggerPerk(owner.Id, owner.SlotIndex, owner.Id, owner.SlotIndex, Id),
                battleCommandFactory.ShowHeal(owner.Id, owner.SlotIndex, owner.Id, owner.SlotIndex, healDelta),
                battleCommandFactory.SetHp(owner.Id, owner.SlotIndex, healthAfter),
            };

            battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.PerkTrigger,
                owner,
                commands,
                owner);

            _coreLog.Information($"[Story][Battle]: Perk resurrection, perkId = {Id}, ownerId = {owner.Id}, healDelta = {healDelta}, health = {healthAfter}, used = {_usedResurrections}, remaining = {ResolveRemaining()}");

            return true;
        }

        private int ResolveRemaining()
        {
            var remaining = _resurrectionsCount - _usedResurrections;

            if (remaining < 0)
                remaining = 0;

            return remaining;
        }
    }
}
