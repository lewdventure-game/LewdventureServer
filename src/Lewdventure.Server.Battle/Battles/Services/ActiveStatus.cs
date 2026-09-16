namespace Server.Battles
{
    internal struct ActiveStatus
    {
        private int _statusId;
        private int _remainingTicks;
        private int _sourceUnitId;
        private float _damageRatio;
        private bool _appliesDamageOverTime;
        private bool _appliesBonuses;
        private IReadOnlyList<RewardBonus> _bonuses;
        private string _bonusSourceKey;

        public int StatusId => _statusId;

        public int RemainingTicks => _remainingTicks;

        public int SourceUnitId => _sourceUnitId;

        public float DamageRatio => _damageRatio;

        public bool AppliesDamageOverTime => _appliesDamageOverTime;

        public bool AppliesBonuses => _appliesBonuses;

        public IReadOnlyList<RewardBonus> Bonuses => _bonuses;

        public string BonusSourceKey => _bonusSourceKey;

        public ActiveStatus(
            int statusId,
            int remainingTicks,
            int sourceUnitId,
            float damageRatio,
            bool appliesDamageOverTime,
            bool appliesBonuses,
            IReadOnlyList<RewardBonus> bonuses,
            string bonusSourceKey)
        {
            _statusId = statusId;
            _remainingTicks = remainingTicks;
            _sourceUnitId = sourceUnitId;
            _damageRatio = damageRatio;
            _appliesDamageOverTime = appliesDamageOverTime;
            _appliesBonuses = appliesBonuses;
            _bonuses = bonuses;
            _bonusSourceKey = bonusSourceKey;
        }

        public void SetRemainingTicks(int remainingTicks)
        {
            _remainingTicks = remainingTicks;
        }

        public void DecrementRemainingTicks()
        {
            if (_remainingTicks <= 0)
                return;

            _remainingTicks -= 1;
        }
    }
}
