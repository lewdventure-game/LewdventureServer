using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    // Summon 3: heavy strike, burn enemy, heal self from damage, grant temporary crit bonus to ally.
    internal sealed class SummonSoulSiphonSkill : BaseSkill
    {
        private const float DamageRatio = 1.25f;
        private const float LifeStealRatio = 0.5f;
        private const int BurningStatusId = 1;
        private const int BurningStacks = 1;
        private const int AllyCritBonusId = 8;
        private const int AllyCritBonusCount = 1;

        internal SummonSoulSiphonSkill(ISkillMapper mapper)
            : base(mapper)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier * DamageRatio;
            var hit = context.TryDealStrike(commands, damageMultiplier, out var dealtDamage, out _);

            if (hit)
            {
                if (context.Target.IsAlive())
                {
                    var burnRewards = context.BattleRewardService.Parse($"status:{BurningStatusId}:{BurningStacks}");
                    context.BattleRewardService.Apply(
                        burnRewards,
                        context.Actor,
                        context.Target,
                        commands,
                        context.CurrentTurn);
                }

                context.Heal(context.Actor, dealtDamage * LifeStealRatio, commands);

                var allyIndex = context.FindAllyMainIndex();

                if (0 <= allyIndex)
                {
                    var ally = context.Attacker.MainUnits[allyIndex];
                    context.BattleBonusService.Grant(
                        ally,
                        AllyCritBonusId,
                        AllyCritBonusCount,
                        $"skill:{SkillKey}",
                        commands,
                        context.CurrentTurn);

                    context.Logger.LogDebug($"[Story][Battle] skill soul siphon ally crit bonus skillId = {SkillKey}, actorId = {context.Actor.Id}, allyId = {ally.Id}, bonusId = {AllyCritBonusId}");
                }
            }

            EndCast(context, commands, context.Target);
        }
    }
}
