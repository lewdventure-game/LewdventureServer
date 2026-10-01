namespace Server.Battles
{
    internal sealed class SkillComponent
    {
        private readonly string _name;
        private readonly IReadOnlyList<SkillParameter> _parameters;

        public SkillComponent(string name, IReadOnlyList<SkillParameter> parameters)
        {
            _name = name;
            _parameters = parameters;
        }

        public string Name => _name;

        public IReadOnlyList<SkillParameter> Parameters => _parameters;

        public bool TryGetParameter(string name, out SkillParameter parameter)
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                if (string.Equals(_parameters[i].Name, name, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                parameter = _parameters[i];

                return true;
            }

            parameter = null!;

            return false;
        }
    }
}
