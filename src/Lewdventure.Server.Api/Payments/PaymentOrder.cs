namespace Server.Api.Payments
{
    internal sealed class PaymentOrder
    {
        public string Id { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Entitlement { get; set; } = string.Empty;
        public string Rewards { get; set; } = string.Empty;
        public uint SteamItemId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public bool Test { get; set; }
        public PaymentOrderState State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime GrantedAt { get; set; }
        public DateTime ReversedAt { get; set; }
        public DateTime NextCheckAt { get; set; }
        public int InitializationAttempts { get; set; }
        public string LastError { get; set; } = string.Empty;
        public bool ReviewRequired { get; set; }
        public string ReviewReason { get; set; } = string.Empty;
    }
}
