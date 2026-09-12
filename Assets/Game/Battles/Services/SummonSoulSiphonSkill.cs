using Microsoft.Extensions.Logging;
using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonSoulSiphonSkill : BaseSkill
    {
        private readonly float _damageRatio;
        private readonly float _lifeStealRatio;
        private readonly string _hitRewards;
        private readonly string _allyRewards;

        internal SummonSoulSiphonSkill(ISkillMapper mapper)
            : base(mapper)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            _damageRatio = ParserUtils.GetFloat(dictionary, "damage_ratio", 0f);
            _lifeStealRatio = ParserUtils.GetFloat(dictionary, "life_steal_ratio", 0f);
            _hitRewards = ParserUtils.GetString(dictionary, "hit_rewards", string.Empty);
            _allyRewards = ParserUtils.GetString(dictionary, "ally_rewards", string.Empty);
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier * _damageRatio;
            var hit = context.TryDealStrike(commands, damageMultiplier, out var dealtDamage, out _);

            if (hit)
            {
                if (context.Target.IsAlive() && string.IsNullOrWhiteSpace(_hitRewards) == false)
                {
                    var hitRewards = context.BattleRewardService.Parse(_hitRewards);
                    context.BattleRewardService.Apply(
                        hitRewards,
                        context.Actor,
                        context.Target,
                        context.Attacker,
                        context.Defender,
                        commands,
                        context.CurrentTurn);
                }

                context.Heal(context.Actor, dealtDamage * _lifeStealRatio, commands);

                var allyIndex = context.FindAllyMainIndex();

                if (0 <= allyIndex && string.IsNullOrWhiteSpace(_allyRewards) == false)
                {
                    var ally = context.Attacker.MainUnits[allyIndex];
                    var allyRewards = context.BattleRewardService.Parse(_allyRewards);
                    context.BattleRewardService.Apply(
                        allyRewards,
                        context.Actor,
                        ally,
                        context.Attacker,
                        context.Defender,
                        commands,
                        context.CurrentTurn);

                    context.Logger.LogDebug($"[Story][Battle]: Skill soul siphon ally rewards, skillId = {SkillKey}, actorId = {context.Actor.Id}, allyId = {ally.Id}");
                }
            }

            EndCast(context, commands, context.Target);
        }
    }
}
