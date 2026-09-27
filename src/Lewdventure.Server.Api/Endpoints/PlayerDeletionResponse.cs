namespace Server.Api.Endpoints
{
    internal sealed class PlayerDeletionResponse
    {
        public string UserId { get; set; } = string.Empty;

        public long Profiles { get; set; }

        public long Runs { get; set; }

        public long Ledger { get; set; }

        public long Idempotency { get; set; }

        public long Users { get; set; }
    }
}
