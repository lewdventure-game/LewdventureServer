namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentFilterDocument
    {
        public bool NewPlayersOnly { get; set; }

        public List<string> Countries { get; set; } = new();
    }
}
