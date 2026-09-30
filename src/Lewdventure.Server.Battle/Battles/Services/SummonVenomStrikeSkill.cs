using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonVenomStrikeSkill : BaseSkill
    {
        private readonly float _damageRatio;
        private readonly string _hitRewards;

        internal SummonVenomStrikeSkill(ISkillMapper mapper, ParserUtils parserUtils)
            : base(mapper, parserUtils)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            parserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            _damageRatio = parserUtils.GetFloat(dictionary, "damage_ratio", 0f);
            _hitRewards = parserUtils.GetString(dictionary, "hit_rewards", string.Empty);
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.SpellMultiplier * _damageRatio;
            var hit = context.TryDealStrike(commands, damageMultiplier, out _, out _);

            if (hit && context.Target.IsAlive() && string.IsNullOrWhiteSpace(_hitRewards) == false)
            {
                var rewards = context.BattleRewardService.Parse(_hitRewards);
                context.BattleRewardService.Apply(
                    rewards,
                    context.Actor,
                    context.Target,
                    context.Attacker,
                    context.Defender,
                    commands,
                    context.CurrentTurn);

                context.CoreLog.Debug($"[Story][Battle]: Skill venom strike applied hit rewards, skillId = {SkillKey}, actorId = {context.Actor.Id}, targetId = {context.Target.Id}");
            }

            EndCast(context, commands, context.Target);
        }
    }
}
