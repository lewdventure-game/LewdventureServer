namespace Server.Api.Payments
{
    internal sealed class PaymentMovement
    {
        public string Id { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Entitlement { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public bool RewardsRecovered { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
