using System.Text.Json.Serialization;

namespace Server.Api.Payments
{
    internal sealed class NutakuPaymentRequest
    {
        public string PaymentId { get; set; } = string.Empty;
        public string SkuId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }

        [JsonRequired]
        public int Test { get; set; }
    }
}
