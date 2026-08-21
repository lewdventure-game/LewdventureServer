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
            var parameters = mapper.Parameters;
            var parts = parameters.Split(';');
            var damageString = parts.Length > 0 ? parts[0] : "1";
            var countString = parts.Length > 1 ? parts[1] : "1";
            var projectileCount = ParserUtils.GetInt(countString, 1);

            if (projectileCount < 1)
                projectileCount = 1;

            _damageRatio = ParserUtils.GetFloat(damageString, 1f);
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
