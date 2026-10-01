namespace Server.Battles
{
    internal sealed class SkillActionContext
    {
        private readonly ISkillExecutionContext _executionContext;
        private readonly SkillComponent _component;
        private readonly IUnitState _target;
        private readonly List<BattleCommand> _commands;
        private readonly int _skillLevel;

        public SkillActionContext(
            ISkillExecutionContext executionContext,
            SkillComponent component,
            IUnitState target,
            List<BattleCommand> commands,
            int skillLevel)
        {
            _executionContext = executionContext;
            _component = component;
            _target = target;
            _commands = commands;
            _skillLevel = skillLevel;
        }

        public ISkillExecutionContext ExecutionContext => _executionContext;

        public SkillComponent Component => _component;

        public IUnitState Target => _target;

        public List<BattleCommand> Commands => _commands;

        public int SkillLevel => _skillLevel;
    }
}
