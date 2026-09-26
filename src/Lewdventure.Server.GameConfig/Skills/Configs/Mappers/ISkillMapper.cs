using Server.Configs;

namespace Server.Skills
{
    internal interface ISkillMapper : IConfigMapper
    {
        public int Id { get; }

        public string Type { get; }

        public int ProcOrder { get; }

        public string Parameters { get; }
    }
}
