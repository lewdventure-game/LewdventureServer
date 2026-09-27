using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonWarHowlSkillCreator : ISkillCreator
    {
        private readonly ParserUtils _parserUtils;

        public SummonWarHowlSkillCreator(ParserUtils parserUtils)
        {
            _parserUtils = parserUtils;
        }

        public SkillType SkillType => SkillType.WarHowl;

        public string TypeKey => "summon_2_skill_1";

        public ISkill Create(ISkillMapper mapper)
        {
            return new SummonWarHowlSkill(mapper, _parserUtils);
        }
    }
}
