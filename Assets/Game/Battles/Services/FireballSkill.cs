using Server.Configs;

namespace Server.Battles
{
    internal sealed class FireballSkill : BaseSkill, IFireballSkill
    {
        private readonly float _damageRatio;
        private readonly int _projectileCount;

        public float DamageRatio => _damageRatio;

        public int ProjectileCount => _projectileCount;

        internal FireballSkill(ISkillMapper mapper)
            : base(mapper)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParserUtils.ParseToDictionary(mapper.Parameters, dictionary);

            var projectileCount = ParserUtils.GetInt(dictionary, "projectile_count", 1);

            if (projectileCount < 1)
                projectileCount = 1;

            _damageRatio = ParserUtils.GetFloat(dictionary, "damage_ratio", 1f);
            _projectileCount = projectileCount;
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier * _damageRatio;

            for (int i = 0; i < _projectileCount; i++)
            {
                if (context.Target.IsAlive() == false)
                    break;

                context.TryDealStrike(commands, damageMultiplier, out _, out _);
            }

            EndCast(context, commands, context.Target);
        }
    }
}
