namespace Server.Admin.Pages.Experiments
{
    public sealed class GroupRowView
    {
        public string Key { get; set; } = string.Empty;

        public GroupInput Input { get; set; } = new();

        public string Placeholder { get; set; } = string.Empty;

        public List<SnapshotOption> Options { get; set; } = new();
    }
}
