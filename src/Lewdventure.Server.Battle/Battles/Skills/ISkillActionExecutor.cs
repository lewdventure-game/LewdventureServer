namespace Server.Battles
{
    internal interface ISkillActionExecutor
    {
        public SkillActionType ActionType { get; }

        public string TypeKey { get; }

        public void Execute(SkillActionContext context);
    }
}
