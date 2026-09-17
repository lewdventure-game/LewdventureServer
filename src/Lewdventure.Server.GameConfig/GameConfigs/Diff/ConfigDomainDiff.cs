namespace Server.GameConfigs
{
    internal sealed class ConfigDomainDiff
    {
        public ConfigDomainDiff(string domain, int rowsBefore, int rowsAfter)
        {
            Domain = domain;
            RowsBefore = rowsBefore;
            RowsAfter = rowsAfter;
        }

        public string Domain { get; }

        public int RowsBefore { get; }

        public int RowsAfter { get; }

        public List<string> Added { get; } = new();

        public List<string> Removed { get; } = new();

        public List<string> Changed { get; } = new();

        public bool HasChanges => 0 < Added.Count || 0 < Removed.Count || 0 < Changed.Count;
    }
}
