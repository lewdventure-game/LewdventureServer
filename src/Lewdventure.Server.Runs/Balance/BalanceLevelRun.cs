namespace Server.Balance
{
    internal sealed class BalanceLevelRun
    {
        public BalanceLevelRun(BalanceLevelResult? result, string error)
        {
            Result = result;
            Error = error;
        }

        public BalanceLevelResult? Result { get; }

        public string Error { get; }
    }
}
