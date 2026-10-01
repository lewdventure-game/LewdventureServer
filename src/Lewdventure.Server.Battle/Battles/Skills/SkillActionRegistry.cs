namespace Server.Battles
{
    internal sealed class SkillActionRegistry : ISkillActionRegistry
    {
        private readonly Dictionary<SkillActionType, ISkillActionExecutor> _executors = new();
        private readonly List<string> _typeKeys = new();

        public SkillActionRegistry(IReadOnlyList<ISkillActionExecutor> executors)
        {
            for (int i = 0; i < executors.Count; i++)
            {
                var executor = executors[i];

                _executors[executor.ActionType] = executor;
                _typeKeys.Add(executor.TypeKey);
            }
        }

        public IReadOnlyList<string> TypeKeys => _typeKeys;

        public bool TryResolve(SkillActionType actionType, out ISkillActionExecutor executor)
        {
            return _executors.TryGetValue(actionType, out executor!);
        }
    }
}
