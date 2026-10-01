namespace Server.Battles
{
    internal sealed class SkillParameter
    {
        private readonly string _name;
        private readonly IReadOnlyList<string> _arguments;

        public SkillParameter(string name, IReadOnlyList<string> arguments)
        {
            _name = name;
            _arguments = arguments;
        }

        public string Name => _name;

        public IReadOnlyList<string> Arguments => _arguments;
    }
}
