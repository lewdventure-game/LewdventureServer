namespace Server.Battles
{
    internal interface ISkillLevelResolver
    {
        public int Resolve(int[] promoteToSkillLevels, int promoteLevel);
    }
}
