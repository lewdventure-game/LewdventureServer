using Server.Statuses;

namespace Server.Battles
{
    internal interface IStatusClassifier
    {
        public bool IsDamageOverTime(StatusType statusType);

        public bool IsStrongDamageOverTime(StatusType statusType);
    }
}
