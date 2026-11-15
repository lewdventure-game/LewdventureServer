namespace Server.Api.Payments
{
    internal sealed class PaymentEntitlement
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Entitlement { get; set; } = string.Empty;
    }
}
