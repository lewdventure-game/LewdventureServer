namespace Server.Shared
{
    public sealed class CoreConfigDomain
    {
        public CoreConfigDomain(string domain, string rowsJson)
        {
            Domain = domain;
            RowsJson = rowsJson;
        }

        public string Domain { get; }

        public string RowsJson { get; }
    }
}
