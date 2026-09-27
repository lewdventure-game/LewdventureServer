using Server.Configs;

namespace Server.Battles
{
    internal sealed class EnergySkillCreator : ISkillCreator
    {
        private readonly ParserUtils _parserUtils;

        public EnergySkillCreator(ParserUtils parserUtils)
        {
            _parserUtils = parserUtils;
        }

        public SkillType SkillType => SkillType.Energy;

        public string TypeKey => "energy";

        public ISkill Create(ISkillMapper mapper)
        {
            return new EnergySkill(mapper, _parserUtils);
        }
    }
}
