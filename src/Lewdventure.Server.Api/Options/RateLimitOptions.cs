namespace Server.Api.Options
{
    internal sealed class RateLimitOptions
    {
        public const string SectionName = "RateLimit";

        public bool Enabled { get; set; } = true;

        public FixedWindowLimitOptions Battle { get; set; } = new() { PermitLimit = 30, WindowSeconds = 10 };

        public int BattleConcurrencyLimit { get; set; }

        public FixedWindowLimitOptions Config { get; set; } = new() { PermitLimit = 6, WindowSeconds = 60 };

        public FixedWindowLimitOptions Admin { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
    }
}
