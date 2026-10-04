using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentCandidate
    {
        public ExperimentCandidate(string experimentId, ExperimentGroupDocument group)
        {
            ExperimentId = experimentId;
            Group = group;
        }

        public string ExperimentId { get; }

        public ExperimentGroupDocument Group { get; }
    }
}
