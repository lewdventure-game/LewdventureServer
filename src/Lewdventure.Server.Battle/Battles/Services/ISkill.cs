namespace Server.Battles
{
    internal interface ISkill
    {
        public int Id { get; }

        public string SkillKey { get; }

        public SkillType SkillType { get; }

        public float CastDurationSeconds { get; }

        public bool RequiresEnergy { get; }

        public bool AllowsInstantActivation { get; }

        public bool IsPassiveBonus { get; }

        public bool CanActivate(SkillActivationContext context);

        public void NotifyActivated(SkillActivationContext context);

        public void Execute(ISkillExecutionContext context);

        public void CollectPassiveBonusIds(int skillLevel, List<int> bonusIds);
    }
}
