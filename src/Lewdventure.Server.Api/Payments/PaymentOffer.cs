namespace Server.Api.Payments
{
    internal sealed class PaymentOffer
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Entitlement { get; set; } = string.Empty;
        public string Rewards { get; set; } = string.Empty;
        public uint SteamItemId { get; set; }
        public Dictionary<string, long> SteamPrices { get; set; } = new(StringComparer.Ordinal);
        public int NutakuGold { get; set; }
    }
}
