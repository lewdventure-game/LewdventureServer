using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    internal sealed class UnknownSkill : BaseSkill
    {
        internal UnknownSkill(ISkillMapper mapper)
            : base(mapper)
        {
        }

        public override void Execute(ISkillExecutionContext context)
        {
            context.Logger.LogError($"[Story][Battle] skill execute unknown skillId = {SkillKey}, actorId = {context.Actor.Id}");
        }
    }
}
