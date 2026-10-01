namespace Server.Battles
{
    internal sealed class SkillRuntimeState
    {
        private readonly int _skillId;

        public SkillRuntimeState(int skillId)
        {
            _skillId = skillId;
        }

        public int SkillId => _skillId;

        public int LastActivationTurn { get; set; } = -1;

        public int ActivationCount { get; set; }

        public bool IsHealthThresholdArmed { get; set; } = true;
    }
}
