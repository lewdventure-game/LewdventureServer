namespace Server.Battles
{
    internal interface ISkillMapper
    {
        public int Id { get; }

        public string SkillKey { get; }

        public SkillType SkillType { get; }

        public string Parameters { get; }
    }
}
