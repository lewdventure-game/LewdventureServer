namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoCollectionNames
    {
        public const string ConfigSnapshots = "config_snapshots";
        public const string ConfigState = "config_state";
        public const string ConfigActivations = "config_activations";
        public const string Users = "users";
        public const string PlayerProfiles = "player_profiles";
        public const string PlayerRuns = "player_runs";
        public const string PlayerLedger = "player_ledger";
        public const string Idempotency = "idempotency";
    }
}
