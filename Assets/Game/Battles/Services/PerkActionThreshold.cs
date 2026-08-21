namespace Server.Battles
{
    internal readonly struct PerkActionThreshold
    {
        private readonly BattlePerkActionType _actionType;
        private readonly int _threshold;

        public BattlePerkActionType ActionType => _actionType;

        public int Threshold => _threshold;

        public PerkActionThreshold(BattlePerkActionType actionType, int threshold)
        {
            _actionType = actionType;
            _threshold = threshold;
        }
    }
}
