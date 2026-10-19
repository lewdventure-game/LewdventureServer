namespace Server.Api.Balance
{
    internal sealed class BalanceJobCreation
    {
        public BalanceJobCreation(string jobId, string error, int statusCode)
        {
            JobId = jobId;
            Error = error;
            StatusCode = statusCode;
        }

        public string JobId { get; }

        public string Error { get; }

        public int StatusCode { get; }
    }
}
