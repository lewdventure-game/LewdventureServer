namespace Server.Api.Payments
{
    internal sealed class PaymentOptions
    {
        public const string SectionName = "Payments";

        public uint SteamAppId { get; set; }
        public string SteamPublisherKey { get; set; } = string.Empty;
        public bool SteamSandbox { get; set; } = true;
        public bool SteamPurchasesEnabled { get; set; }

        public string NutakuTitleId { get; set; } = string.Empty;
        public string NutakuServerKey { get; set; } = string.Empty;
        public bool NutakuSandbox { get; set; } = true;
        public bool NutakuPurchasesEnabled { get; set; }

        public List<PaymentOffer> Offers { get; set; } = new();

        public string SteamScope => FormatScope("steam", SteamAppId.ToString(System.Globalization.CultureInfo.InvariantCulture), SteamSandbox);
        public string NutakuScope => FormatScope("nutaku", NutakuTitleId, NutakuSandbox);
        public bool Configured => SteamAppId != 0 || NutakuTitleId.Length != 0;

        private string FormatScope(string platform, string titleId, bool sandbox)
        {
            return $"{platform}:{titleId}:{(sandbox ? "sandbox" : "live")}";
        }
    }
}
