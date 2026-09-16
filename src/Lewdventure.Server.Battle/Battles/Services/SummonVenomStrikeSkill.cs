using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    // Summon 1: strike + poison stacks on the enemy.
    internal sealed class SummonVenomStrikeSkill : BaseSkill
    {
        private const float DamageRatio = 1f;
        private const int PoisonStatusId = 3;
        private const int PoisonStacks = 2;

        internal SummonVenomStrikeSkill(ISkillMapper mapper)
            : base(mapper)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier * DamageRatio;
            var hit = context.TryDealStrike(commands, damageMultiplier, out _, out _);

            if (hit && context.Target.IsAlive())
            {
                var rewards = context.BattleRewardService.Parse($"status:{PoisonStatusId}:{PoisonStacks}");
                context.BattleRewardService.Apply(
                    rewards,
                    context.Actor,
                    context.Target,
                    commands,
                    context.CurrentTurn);

                context.Logger.LogDebug($"[Story][Battle] skill venom strike applied poison skillId = {SkillKey}, actorId = {context.Actor.Id}, targetId = {context.Target.Id}, stacks = {PoisonStacks}");
            }

            EndCast(context, commands, context.Target);
        }
    }
}
