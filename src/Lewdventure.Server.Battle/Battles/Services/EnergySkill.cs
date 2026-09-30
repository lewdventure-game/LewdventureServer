using Server.Configs;

namespace Server.Battles
{
    internal sealed class EnergySkill : BaseSkill
    {
        internal EnergySkill(ISkillMapper mapper, ParserUtils parserUtils)
            : base(mapper, parserUtils)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.SpellMultiplier;
            context.TryDealStrike(commands, damageMultiplier, out _, out _);

            EndCast(context, commands, context.Target);
        }
    }
}
