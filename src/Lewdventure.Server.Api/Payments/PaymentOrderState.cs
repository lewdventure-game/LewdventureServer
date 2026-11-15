namespace Server.Api.Payments
{
    internal enum PaymentOrderState
    {
        Created,
        Initiating,
        AwaitingApproval,
        Failed,
        Granted,
        Reversed,
    }
}
