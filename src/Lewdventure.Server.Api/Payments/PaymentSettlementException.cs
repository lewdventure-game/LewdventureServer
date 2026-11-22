namespace Server.Api.Payments
{
    internal sealed class PaymentSettlementException : Exception
    {
        public PaymentSettlementException(bool retryable) : base("Payment settlement could not be completed.")
        {
            Retryable = retryable;
        }

        public bool Retryable { get; }
    }
}
