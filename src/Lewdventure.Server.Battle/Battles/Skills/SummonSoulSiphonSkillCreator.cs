using Server.Configs;

namespace Server.Battles
{
    internal sealed class SummonSoulSiphonSkillCreator : ISkillCreator
    {
        private readonly ParserUtils _parserUtils;

        public SummonSoulSiphonSkillCreator(ParserUtils parserUtils)
        {
            _parserUtils = parserUtils;
        }

        public SkillType SkillType => SkillType.SoulSiphon;

        public string TypeKey => "summon_3_skill_1";

        public ISkill Create(ISkillMapper mapper)
        {
            return new SummonSoulSiphonSkill(mapper, _parserUtils);
        }
    }
}
