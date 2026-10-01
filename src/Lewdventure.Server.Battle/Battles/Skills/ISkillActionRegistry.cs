namespace Server.Battles
{
    internal interface ISkillActionRegistry
    {
        public IReadOnlyList<string> TypeKeys { get; }

        public bool TryResolve(SkillActionType actionType, out ISkillActionExecutor executor);
    }
}
