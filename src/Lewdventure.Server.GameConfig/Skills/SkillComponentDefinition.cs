namespace Server.Skills
{
    internal sealed class SkillComponentDefinition
    {
        public SkillComponentDefinition(string name, IReadOnlyList<string> parameterNames)
        {
            Name = name;
            ParameterNames = parameterNames;
        }

        public string Name { get; }

        public IReadOnlyList<string> ParameterNames { get; }

        public bool HasParameter(string parameterName)
        {
            for (int i = 0; i < ParameterNames.Count; i++)
            {
                if (string.Equals(ParameterNames[i], parameterName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
