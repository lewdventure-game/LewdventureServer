namespace Server.Battles
{
    internal sealed class UnknownSkill : ISkill
    {
        private readonly int _id;
        private readonly string _skillKey;

        internal UnknownSkill(int id, string skillKey)
        {
            _id = id;
            _skillKey = skillKey;
        }

        public int Id => _id;

        public string SkillKey => _skillKey;

        public SkillType SkillType => SkillType.Unknown;

        public float CastDurationSeconds => 0f;

        public bool RequiresEnergy => false;

        public bool AllowsInstantActivation => false;

        public bool IsPassiveBonus => false;

        public bool CanActivate(SkillActivationContext context)
        {
            return false;
        }

        public void NotifyActivated(SkillActivationContext context)
        {
        }

        public void Execute(ISkillExecutionContext context)
        {
            context.CoreLog.Error($"[Story][Battle]: Skill execute unknown, skillId = {SkillKey}, actorId = {context.Actor.Id}");
        }

        public void CollectPassiveBonusIds(int skillLevel, List<int> bonusIds)
        {
        }

        public float ResolveRequiredEnergy(int skillLevel)
        {
            return 0f;
        }
    }
}
