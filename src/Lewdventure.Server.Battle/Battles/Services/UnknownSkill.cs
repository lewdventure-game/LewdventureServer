using Server.Configs;

namespace Server.Battles
{
    internal sealed class UnknownSkill : BaseSkill
    {
        internal UnknownSkill(ISkillMapper mapper, ParserUtils parserUtils)
            : base(mapper, parserUtils)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            context.CoreLog.Error($"[Story][Battle]: Skill execute unknown, skillId = {SkillKey}, actorId = {context.Actor.Id}");
        }
    }
}
