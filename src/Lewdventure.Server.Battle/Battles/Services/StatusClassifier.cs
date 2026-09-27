using Server.Statuses;

namespace Server.Battles
{
    internal sealed class StatusClassifier : IStatusClassifier
    {
        public bool IsDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.Burning
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.Poison
                || statusType == StatusType.PoisonStrong;
        }

        public bool IsStrongDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.BurningStrong
                || statusType == StatusType.PoisonStrong;
        }
    }
}
