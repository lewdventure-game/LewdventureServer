namespace Server.Api.Payments
{
    internal sealed class PaymentAuditEvent
    {
        public string Id { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
