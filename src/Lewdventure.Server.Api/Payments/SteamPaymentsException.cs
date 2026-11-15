namespace Server.Api.Payments
{
    internal sealed class SteamPaymentsException : Exception
    {
        public SteamPaymentsException() : base("Steam could not confirm the request.")
        {
        }
    }
}
