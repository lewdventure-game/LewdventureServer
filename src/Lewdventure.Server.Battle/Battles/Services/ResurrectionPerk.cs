using Microsoft.Extensions.Logging;
using Server.Perks;

namespace Server.Battles
{
    internal sealed class ResurrectionPerk : BasePerk
    {
        private readonly float _healthRatio;
        private int _remainingResurrections;
        private readonly ILogger _logger;

        internal ResurrectionPerk(
            IPerkMapper mapper,
            float healthRatio,
            int resurrectionsCount,
            ILogger logger)
            : base(mapper)
        {
            _healthRatio = healthRatio;
            _remainingResurrections = resurrectionsCount;
            _logger = logger;
        }

        public override bool CanTrigger(int currentTurn)
        {
            return 0 < _remainingResurrections;
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
            if (_remainingResurrections <= 0)
                return false;

            if (0 < owner.CharacteristicState.Health)
            {
                _logger.LogDebug($"[Story][Battle] perk resurrection skipped alive perkId = {Id}, ownerId = {owner.Id}");

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
            _remainingResurrections -= 1;

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

            _logger.LogInformation($"[Story][Battle] perk resurrection perkId = {Id}, ownerId = {owner.Id}, healDelta = {healDelta}, health = {healthAfter}, remaining = {_remainingResurrections}");

            if (_remainingResurrections == 0)
                RemoveSelfFromOwner(owner);

            return true;
        }

        private void RemoveSelfFromOwner(IUnitState owner)
        {
            var perks = owner.Perks;

            for (int i = 0; i < perks.Count; i++)
            {
                if (ReferenceEquals(perks[i], this) == false)
                    continue;

                perks.RemoveAt(i);
                _logger.LogWarning($"[Story][Battle] resurrection perk removed exhausted perkId = {Id} ownerId = {owner.Id}");

                return;
            }
        }
    }
}
