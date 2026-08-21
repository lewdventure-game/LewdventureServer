namespace Server.Battles
{
    internal sealed class SkillMapper : ISkillMapper
    {
        private readonly int _id;
        private readonly string _skillKey;
        private readonly SkillType _skillType;
        private readonly string _parameters;

        public int Id => _id;

        public string SkillKey => _skillKey;

        public SkillType SkillType => _skillType;

        public string Parameters => _parameters;

        internal SkillMapper(
            int id,
            string skillKey,
            SkillType skillType,
            string parameters)
        {
            _id = id;
            _skillKey = skillKey;
            _skillType = skillType;
            _parameters = parameters;
        }
    }
}
