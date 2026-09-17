using Microsoft.Extensions.Logging;
using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonVenomStrikeSkill : BaseSkill
    {
        private readonly float _damageRatio;
        private readonly string _hitRewards;

        internal SummonVenomStrikeSkill(ISkillMapper mapper)
            : base(mapper)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            _damageRatio = ParserUtils.GetFloat(dictionary, "damage_ratio", 0f);
            _hitRewards = ParserUtils.GetString(dictionary, "hit_rewards", string.Empty);
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier * _damageRatio;
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

                context.Logger.LogDebug($"[Story][Battle]: Skill venom strike applied hit rewards, skillId = {SkillKey}, actorId = {context.Actor.Id}, targetId = {context.Target.Id}");
            }

            EndCast(context, commands, context.Target);
        }
    }
}
