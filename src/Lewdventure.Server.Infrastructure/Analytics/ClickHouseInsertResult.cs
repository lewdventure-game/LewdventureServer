namespace Server.Infrastructure.Analytics
{
    internal sealed class ClickHouseInsertResult
    {
        public ClickHouseInsertResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }

        public string Error { get; }
    }
}
