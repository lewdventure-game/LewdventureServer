namespace Server.Api.Payments
{
    internal sealed class SteamCheckoutRequest
    {
        public string RequestKey { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public long Amount { get; set; }
    }
}
