namespace Server.Battles
{
    internal interface ISkillCreator
    {
        public SkillType SkillType { get; }

        public string TypeKey { get; }

        public ISkill Create(ISkillMapper mapper);
    }
}
