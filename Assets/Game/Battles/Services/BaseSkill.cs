using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    internal abstract class BaseSkill : ISkill
    {
        private readonly int _id;
        private readonly string _skillKey;
        private readonly SkillType _skillType;

        public int Id => _id;

        public string SkillKey => _skillKey;

        public SkillType SkillType => _skillType;

        protected BaseSkill(ISkillMapper mapper)
        {
            _id = mapper.Id;
            _skillKey = mapper.SkillKey;
            _skillType = mapper.SkillType;
        }

        public abstract void Execute(ISkillExecutionContext context);

        protected void BeginCast(ISkillExecutionContext context, List<BattleCommand> commands)
        {
            commands.Add(context.BattleCommandFactory.CastSkill(context.Actor.Id, context.Actor.SlotIndex, context.Target.Id, context.Target.SlotIndex, SkillKey));
            commands.Add(context.BattleCommandFactory.PlayAnimation(context.Actor.Id, context.Actor.SlotIndex, "cast"));
        }

        protected void EndCast(ISkillExecutionContext context, List<BattleCommand> commands, IUnitState target)
        {
            if (context.Phase == BattlePhaseType.UnitSkill)
            {
                context.Logger.LogDebug($"[Story][Battle]: Skill EndCast skip Wait unit phase, skillKey = {SkillKey}, unitId = {context.Actor.Id}");
                context.AddStep(commands, target);

                return;
            }

            commands.Add(context.BattleCommandFactory.Wait(context.Cooldown));
            context.AddStep(commands, target);
        }
    }
}
