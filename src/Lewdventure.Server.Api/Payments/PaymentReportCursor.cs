namespace Server.Api.Payments
{
    internal sealed class PaymentReportCursor
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public DateTime LastSuccessAt { get; set; }
        public string LastError { get; set; } = string.Empty;
    }
}
