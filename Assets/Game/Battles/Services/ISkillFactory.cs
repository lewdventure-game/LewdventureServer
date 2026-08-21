namespace Server.Battles
{
    internal interface ISkillFactory
    {
        public ISkill Create(string skillId);

        public bool IsKnownSkillId(string skillId);
    }
}
