using Microsoft.Extensions.Logging;
using Server.Perks;
using Server.Statuses;

namespace Server.Battles
{
    internal abstract class ElementalAttackPerk : BasePerk
    {
        private readonly int _projectileCount;
        private readonly float _damageRatio;
        private readonly IReadOnlyList<BattleReward> _hitRewards;
        private readonly float _rewardsChance;
        private readonly float _debuffRewardsChance;
        private readonly IReadOnlyList<int> _procRounds;

        protected ElementalAttackPerk(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
            : base(mapper)
        {
            _projectileCount = projectileCount;
            _damageRatio = damageRatio;
            _hitRewards = hitRewards;
            _rewardsChance = rewardsChance;
            _debuffRewardsChance = debuffRewardsChance;
            _procRounds = procRounds;
        }

        protected IReadOnlyList<BattleReward> HitRewards => _hitRewards;

        protected float RewardsChance => _rewardsChance;

        protected float DebuffRewardsChance => _debuffRewardsChance;

        protected virtual bool UsesFlatHitRewards => true;

        public override bool CanTrigger(int currentTurn)
        {
            if (_procRounds.Count == 0)
                return false;

            for (int i = 0; i < _procRounds.Count; i++)
            {
                // Sheets/GDD rounds are 1-based; simulator turns are 0-based.
                if (_procRounds[i] == currentTurn + 1)
                    return true;
            }

            return false;
        }

        public override void Trigger(IPerkExecutionContext context)
        {
            var owner = context.Owner;

            if (owner.IsAlive() == false)
                return;

            var targetIndex = context.FindDefenderTargetIndex();

            if (targetIndex < 0)
            {
                context.Logger.LogWarning($"[Story][Battle]: Perk elemental no target, perkId = {Id}, ownerId = {owner.Id}");

                return;
            }

            var target = context.Defender.MainUnits[targetIndex];
            var commands = new List<BattleCommand>();
            var projectileCount = _projectileCount;

            if (projectileCount < 1)
                projectileCount = 1;

            commands.Add(context.BattleCommandFactory.TriggerPerk(owner.Id, owner.SlotIndex, target.Id, target.SlotIndex, Id));
            commands.Add(context.BattleCommandFactory.PlayAnimation(owner.Id, owner.SlotIndex, "cast"));

            context.Logger.LogDebug($"[Story][Battle]: Perk elemental trigger, perkId = {Id}, type = {PerkType}, ownerId = {owner.Id}, targetId = {target.Id}, projectiles = {projectileCount}, damageRatio = {_damageRatio}, turn = {context.CurrentTurn}");

            var anyDamageHits = 0;

            for (int i = 0; i < projectileCount; i++)
            {
                if (target.IsAlive() == false)
                    break;

                if (ResolveProjectile(context, owner, target, commands))
                    anyDamageHits += 1;
            }

            AppendIdleIfAlive(context, commands, owner);

            context.BattleScriptBuilder.Add(
                context.Steps,
                context.CurrentTurn,
                BattlePhaseType.PerkTrigger,
                owner,
                commands,
                target);

            if (0 < anyDamageHits)
            {
                context.Logger.LogDebug($"[Story][Battle]: any_damage flush after elemental step, perkId = {Id}, ownerId = {owner.Id}, targetId = {target.Id}, count = {anyDamageHits}, turn = {context.CurrentTurn}");

                for (int i = 0; i < anyDamageHits; i++)
                    context.NotifyAnyDamage();
            }

            if (target.IsAlive() == false)
                context.EmitDeath(target);
        }

        private bool ResolveProjectile(
            IPerkExecutionContext context,
            IUnitState owner,
            IUnitState target,
            List<BattleCommand> commands)
        {
            var random = context.SeededRandomService;
            var ownerCharacteristics = owner.CharacteristicState;
            var targetCharacteristics = target.CharacteristicState;
            var evasionRoll = random.GetRandomValue();
            var isEvaded = evasionRoll < targetCharacteristics.Evasion;

            context.Logger.LogDebug($"[Story][Battle]: Perk projectile, perkId = {Id}, ownerId = {owner.Id}, targetId = {target.Id}, evasionRoll = {evasionRoll}, evasion = {targetCharacteristics.Evasion}, isEvaded = {isEvaded}");

            if (isEvaded)
            {
                commands.Add(context.BattleCommandFactory.ShowMiss(owner.Id, owner.SlotIndex, target.Id, target.SlotIndex));
                AppendMissWait(context, commands);

                return false;
            }

            var criticalRoll = random.GetRandomValue();
            var isCritical = criticalRoll < ownerCharacteristics.CriticalChance;

            context.Logger.LogDebug($"[Story][Battle]: Perk projectile, perkId = {Id}, ownerId = {owner.Id}, criticalRoll = {criticalRoll}, criticalChance = {ownerCharacteristics.CriticalChance}, isCritical = {isCritical}");

            var defenceFactor = 1f - targetCharacteristics.Defence;

            if (defenceFactor < 0f)
                defenceFactor = 0f;

            var damage = ownerCharacteristics.Damage * _damageRatio;

            if (isCritical)
                damage *= ownerCharacteristics.CriticalMultiplier;

            damage *= defenceFactor;

            var healthAfter = targetCharacteristics.Health - damage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            targetCharacteristics.Health = healthAfter;

            commands.Add(context.BattleCommandFactory.ShowDamage(owner.Id, owner.SlotIndex, target.Id, target.SlotIndex, damage, isCritical, false));
            commands.Add(context.BattleCommandFactory.SetHp(target.Id, target.SlotIndex, healthAfter));

            context.Logger.LogInformation($"[Story][Battle]: Perk projectile damage, perkId = {Id}, ownerId = {owner.Id}, targetId = {target.Id}, damage = {damage}, isCritical = {isCritical}, health = {healthAfter}");

            if (UsesFlatHitRewards)
                TryApplyHitRewards(context, owner, target, commands);

            OnProjectileHit(context, owner, target, commands);

            context.Logger.LogDebug($"[Story][Battle]: any_damage deferred until elemental step commit, perkId = {Id} ownerId = {owner.Id} targetId = {target.Id}");

            return true;
        }

        protected virtual void OnProjectileHit(
            IPerkExecutionContext context,
            IUnitState owner,
            IUnitState target,
            List<BattleCommand> commands)
        {
        }

        protected virtual float ResolveFlatHitRewardsChance(IPerkExecutionContext context, IUnitState target)
        {
            return _rewardsChance;
        }

        protected void ApplyHitRewardsTimes(
            IPerkExecutionContext context,
            IUnitState owner,
            IUnitState target,
            List<BattleCommand> commands,
            int times)
        {
            if (times <= 0 || _hitRewards.Count == 0)
                return;

            for (int i = 0; i < times; i++)
            {
                context.BattleRewardService.Apply(_hitRewards, owner, target, context.Attacker, context.Defender, commands, context.CurrentTurn);
                context.Logger.LogDebug($"[Story][Battle]: Perk elemental extra reward apply, perkId = {Id} index = {i + 1} of {times}");
            }
        }

        protected int CountStatusesOfTypes(IPerkExecutionContext context, IUnitState target, bool burn, bool poison)
        {
            var activeStatuses = target.ActiveStatuses;
            var count = 0;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (MatchesStatusFamily(context, activeStatuses[i].StatusId, burn, poison) == false)
                    continue;

                count += 1;
            }

            return count;
        }

        protected int CleanseStatusesOfTypes(
            IPerkExecutionContext context,
            IUnitState target,
            List<BattleCommand> commands,
            bool burn,
            bool poison)
        {
            var activeStatuses = target.ActiveStatuses;
            var cleansed = 0;

            for (int i = activeStatuses.Count - 1; 0 <= i; i--)
            {
                var activeStatus = activeStatuses[i];

                if (MatchesStatusFamily(context, activeStatus.StatusId, burn, poison) == false)
                    continue;

                if (activeStatus.AppliesBonuses)
                    context.BattleBonusService.RemoveBySourceKey(target, activeStatus.BonusSourceKey, commands, context.CurrentTurn);

                commands.Add(context.BattleCommandFactory.RemoveStatus(target.Id, target.SlotIndex, activeStatus.StatusId));
                activeStatuses.RemoveAt(i);
                cleansed += 1;
            }

            context.Logger.LogDebug($"[Story][Battle]: Perk elemental cleanse, perkId = {Id}, targetId = {target.Id}, burn = {burn}, poison = {poison}, cleansed = {cleansed}");

            return cleansed;
        }

        protected bool TryRollRewardsChance(
            IPerkExecutionContext context,
            IUnitState target,
            float chance,
            string reason)
        {
            var rollChance = chance;

            if (1f < rollChance)
            {
                context.Logger.LogWarning($"[Story][Battle]: Perk rewards chance clamped, perkId = {Id}, reason = {reason}, rawChance = {chance}");
                rollChance = 1f;
            }

            if (rollChance <= 0f)
            {
                context.Logger.LogDebug($"[Story][Battle]: Perk rewards chance skip, perkId = {Id}, reason = {reason}, chance = {rollChance}");

                return false;
            }

            var rewardsRoll = context.SeededRandomService.GetRandomValue();
            var success = rewardsRoll < rollChance;

            context.Logger.LogDebug($"[Story][Battle]: Perk rewards roll, perkId = {Id}, reason = {reason}, rewardsRoll = {rewardsRoll}, chance = {rollChance}, success = {success}, targetId = {target.Id}");

            return success;
        }

        private bool MatchesStatusFamily(IPerkExecutionContext context, int statusId, bool burn, bool poison)
        {
            if (context.ConfigDistributor.Statuses.TryGet(statusId, out var statusMapper) == false)
                return false;

            var statusType = statusMapper.StatusType;
            var isBurn = burn && (statusType == StatusType.Burning || statusType == StatusType.BurningStrong);

            if (isBurn)
                return true;

            var isPoison = poison && (statusType == StatusType.Poison || statusType == StatusType.PoisonStrong);

            return isPoison;
        }

        private void TryApplyHitRewards(
            IPerkExecutionContext context,
            IUnitState owner,
            IUnitState target,
            List<BattleCommand> commands)
        {
            if (_hitRewards.Count == 0)
                return;

            var effectiveChance = ResolveFlatHitRewardsChance(context, target);

            if (TryRollRewardsChance(context, target, effectiveChance, "flat_hit") == false)
            {
                context.Logger.LogDebug($"[Story][Battle]: Perk hit rewards skipped, perkId = {Id}, targetId = {target.Id}");

                return;
            }

            context.BattleRewardService.Apply(_hitRewards, owner, target, context.Attacker, context.Defender, commands, context.CurrentTurn);
            context.Logger.LogDebug($"[Story][Battle]: Perk hit rewards applied, perkId = {Id}, targetId = {target.Id}");
        }

        private void AppendIdleIfAlive(IPerkExecutionContext context, List<BattleCommand> commands, IUnitState unit)
        {
            if (unit.IsAlive() == false)
                return;

            commands.Add(context.BattleCommandFactory.PlayAnimation(unit.Id, unit.SlotIndex, "idle"));
            context.Logger.LogDebug($"[Story][Battle]: Perk idle after cast, perkId = {Id}, ownerId = {unit.Id}");
        }

        private void AppendMissWait(IPerkExecutionContext context, List<BattleCommand> commands)
        {
            if (context.ConfigDistributor.Constants.TryGet(ConstantKeys.BattleFlytextTimerKey, out var constant) == false)
                return;

            if (float.TryParse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture, out var timer) == false)
                return;

            if (timer <= 0f)
                return;

            commands.Add(context.BattleCommandFactory.Wait(timer));
            context.Logger.LogDebug($"[Story][Battle]: Perk miss wait, perkId = {Id}, seconds = {timer}");
        }
    }
}
