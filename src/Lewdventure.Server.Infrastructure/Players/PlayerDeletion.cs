namespace Server.Infrastructure.Players
{
    internal sealed class PlayerDeletion
    {
        public PlayerDeletion(long profiles, long runs, long ledger, long idempotency, long users)
        {
            Profiles = profiles;
            Runs = runs;
            Ledger = ledger;
            Idempotency = idempotency;
            Users = users;
        }

        public long Profiles { get; }

        public long Runs { get; }

        public long Ledger { get; }

        public long Idempotency { get; }

        public long Users { get; }

        public bool HasData => 0 < Profiles || 0 < Runs || 0 < Ledger || 0 < Users;
    }
}
