namespace Server.Api.Payments
{
    internal sealed class PaymentAccount
    {
        public string Id { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
