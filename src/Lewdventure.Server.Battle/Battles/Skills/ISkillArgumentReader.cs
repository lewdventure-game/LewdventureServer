namespace Server.Battles
{
    internal interface ISkillArgumentReader
    {
        public bool TryReadFloat(SkillComponent component, string parameterName, int skillLevel, out float value);

        public bool TryReadInt(SkillComponent component, string parameterName, int skillLevel, out int value);

        public bool ReadFlag(SkillComponent component, string parameterName, int skillLevel);

        public bool TryReadArgument(SkillComponent component, string parameterName, int skillLevel, out string argument);
    }
}
