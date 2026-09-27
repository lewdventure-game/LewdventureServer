using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonVenomStrikeSkillCreator : ISkillCreator
    {
        private readonly ParserUtils _parserUtils;

        public SummonVenomStrikeSkillCreator(ParserUtils parserUtils)
        {
            _parserUtils = parserUtils;
        }

        public SkillType SkillType => SkillType.VenomStrike;

        public string TypeKey => "summon_1_skill_1";

        public ISkill Create(ISkillMapper mapper)
        {
            return new SummonVenomStrikeSkill(mapper, _parserUtils);
        }
    }
}
