namespace Server.Api.Payments
{
    internal sealed class PaymentReviewRequest
    {
        public string Reference { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
    }
}
