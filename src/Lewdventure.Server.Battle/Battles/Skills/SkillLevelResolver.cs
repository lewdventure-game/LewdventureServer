namespace Server.Battles
{
    internal sealed class SkillLevelResolver : ISkillLevelResolver
    {
        public int Resolve(int[] promoteToSkillLevels, int promoteLevel)
        {
            if (promoteToSkillLevels.Length == 0)
                return 0;

            var skillLevel = 0;

            for (int i = 0; i < promoteToSkillLevels.Length; i++)
            {
                if (promoteLevel < promoteToSkillLevels[i])
                    break;

                skillLevel = i;
            }

            return skillLevel;
        }
    }
}
