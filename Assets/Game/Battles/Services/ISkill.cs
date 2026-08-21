namespace Server.Battles
{
    internal interface ISkill
    {
        public int Id { get; }

        public string SkillKey { get; }

        public SkillType SkillType { get; }

        public void Execute(ISkillExecutionContext context);
    }
}
