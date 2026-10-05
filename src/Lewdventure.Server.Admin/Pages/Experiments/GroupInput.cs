namespace Server.Admin.Pages.Experiments
{
    public sealed class GroupInput
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string SnapshotVersion { get; set; } = string.Empty;

        public string? Percent { get; set; }

        public bool NewPlayersOnly { get; set; }

        public string Countries { get; set; } = string.Empty;
    }
}
