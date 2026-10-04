using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentSummary
    {
        public ExperimentSummary(ExperimentDocument experiment)
        {
            Experiment = experiment;
        }

        public ExperimentDocument Experiment { get; }

        public Dictionary<string, long> Participants { get; } = new(StringComparer.Ordinal);
    }
}
