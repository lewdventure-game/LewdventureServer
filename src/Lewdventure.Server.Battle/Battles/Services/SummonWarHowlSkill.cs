using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    // Summon 2: chip damage, heal ally main, grant damage bonus, apply crit-status to ally.
    internal sealed class SummonWarHowlSkill : BaseSkill
    {
        private const float ChipDamageRatio = 0.35f;
        private const float AllyHealFromMaxRatio = 0.12f;
        // Bonuses id=2 = damage_local (+5), if_equipped:characters:1. Not id=1 (max_health_perk).
        private const int AllyDamageBonusId = 2;
        private const int AllyDamageBonusCount = 1;
        private const int AllyCritStatusId = 5;

        internal SummonWarHowlSkill(ISkillMapper mapper)
            : base(mapper)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            if (context.Target.IsAlive())
            {
                var chipMultiplier = context.Actor.CharacteristicState.SkillMultiplier * ChipDamageRatio;
                context.TryDealStrike(commands, chipMultiplier, out _, out _);
            }

            var allyIndex = context.FindAllyMainIndex();

            if (allyIndex < 0)
            {
                context.Logger.LogWarning($"[Story][Battle] skill war howl no ally main skillId = {SkillKey}, actorId = {context.Actor.Id}");
                EndCast(context, commands, context.Target);

                return;
            }

            var ally = context.Attacker.MainUnits[allyIndex];
            var healAmount = ally.CharacteristicState.MaxHealth * AllyHealFromMaxRatio;
            context.Heal(ally, healAmount, commands);

            context.BattleBonusService.Grant(
                ally,
                AllyDamageBonusId,
                AllyDamageBonusCount,
                $"skill:{SkillKey}",
                commands,
                context.CurrentTurn);

            var statusRewards = context.BattleRewardService.Parse($"status:{AllyCritStatusId}:1");
            context.BattleRewardService.Apply(
                statusRewards,
                ally,
                context.Target,
                commands,
                context.CurrentTurn);

            context.Logger.LogDebug($"[Story][Battle] skill war howl buffed ally skillId = {SkillKey}, actorId = {context.Actor.Id}, allyId = {ally.Id}, heal = {healAmount}, bonusId = {AllyDamageBonusId}, statusId = {AllyCritStatusId}");

            EndCast(context, commands, ally);
        }
    }
}
