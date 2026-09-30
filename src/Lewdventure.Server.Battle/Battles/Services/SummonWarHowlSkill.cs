using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonWarHowlSkill : BaseSkill
    {
        private readonly float _damageRatio;
        private readonly float _healFromMaxRatio;
        private readonly string _allyRewards;

        internal SummonWarHowlSkill(ISkillMapper mapper, ParserUtils parserUtils)
            : base(mapper, parserUtils)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            parserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            _damageRatio = parserUtils.GetFloat(dictionary, "damage_ratio", 0f);
            _healFromMaxRatio = parserUtils.GetFloat(dictionary, "heal_from_max_ratio", 0f);
            _allyRewards = parserUtils.GetString(dictionary, "ally_rewards", string.Empty);
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            if (context.Target.IsAlive())
            {
                var chipMultiplier = context.SpellMultiplier * _damageRatio;
                context.TryDealStrike(commands, chipMultiplier, out _, out _);
            }

            var allyIndex = context.FindAllyMainIndex();

            if (allyIndex < 0)
            {
                context.CoreLog.Warning($"[Story][Battle]: Skill war howl no ally main, skillId = {SkillKey}, actorId = {context.Actor.Id}");
                EndCast(context, commands, context.Target);

                return;
            }

            var ally = context.Attacker.MainUnits[allyIndex];
            var healAmount = ally.CharacteristicState.MaxHealth * _healFromMaxRatio;
            context.Heal(ally, healAmount, commands);

            if (string.IsNullOrWhiteSpace(_allyRewards) == false)
            {
                var rewards = context.BattleRewardService.Parse(_allyRewards);
                context.BattleRewardService.Apply(
                    rewards,
                    context.Actor,
                    ally,
                    context.Attacker,
                    context.Defender,
                    commands,
                    context.CurrentTurn);
            }

            context.CoreLog.Debug($"[Story][Battle]: Skill war howl buffed ally, skillId = {SkillKey}, actorId = {context.Actor.Id}, allyId = {ally.Id}, heal = {healAmount}");

            EndCast(context, commands, ally);
        }
    }
}
