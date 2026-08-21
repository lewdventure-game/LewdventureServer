namespace Server.Battles
{
    internal sealed class EnergySkill : BaseSkill
    {
        internal EnergySkill(ISkillMapper mapper)
            : base(mapper)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            var commands = new List<BattleCommand>();
            BeginCast(context, commands);

            var damageMultiplier = context.Actor.CharacteristicState.SkillMultiplier;
            context.TryDealStrike(commands, damageMultiplier, out _, out _);

            EndCast(context, commands, context.Target);
        }
    }
}
