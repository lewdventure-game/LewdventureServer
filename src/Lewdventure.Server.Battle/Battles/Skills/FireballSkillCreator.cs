using Server.Configs;

namespace Server.Battles
{
    internal sealed class FireballSkillCreator : ISkillCreator
    {
        private readonly ParserUtils _parserUtils;

        public FireballSkillCreator(ParserUtils parserUtils)
        {
            _parserUtils = parserUtils;
        }

        public SkillType SkillType => SkillType.Fireball;

        public string TypeKey => "fireball";

        public ISkill Create(ISkillMapper mapper)
        {
            return new FireballSkill(mapper, _parserUtils);
        }
    }
}
