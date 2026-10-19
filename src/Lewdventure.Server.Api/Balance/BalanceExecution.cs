using Server.Balance;

namespace Server.Api.Balance
{
    internal sealed class BalanceExecution
    {
        public BalanceExecution(IBalanceRunResult? result, string error, int statusCode, string configVersion, int edits, string summary, string scenario)
        {
            Result = result;
            Error = error;
            StatusCode = statusCode;
            ConfigVersion = configVersion;
            Edits = edits;
            Summary = summary;
            Scenario = scenario;
        }

        public IBalanceRunResult? Result { get; }

        public string Error { get; }

        public int StatusCode { get; }

        public string ConfigVersion { get; }

        public int Edits { get; }

        public string Summary { get; }

        public string Scenario { get; }
    }
}
