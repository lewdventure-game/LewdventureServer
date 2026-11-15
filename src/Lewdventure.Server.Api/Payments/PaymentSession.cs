namespace Server.Api.Payments
{
    internal sealed record PaymentSession(string UserId, string Scope, string Subject, bool Steam);
}
