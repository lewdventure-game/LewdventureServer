namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunPendingChoiceDocument
    {
        public const string PerkKind = "perk";
        public const string ForkKind = "fork";

        public string Kind { get; set; } = string.Empty;

        public List<int> Options { get; set; } = new();

        public int ChoiceCount { get; set; } = 1;
    }
}
