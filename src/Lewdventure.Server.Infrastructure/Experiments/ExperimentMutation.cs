using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentMutation
    {
        public ExperimentMutation(
            ExperimentDocument experiment,
            string groupId,
            string actor,
            string reason,
            DateTime now,
            ExperimentOperationResult result,
            CancellationToken cancellationToken)
        {
            Experiment = experiment;
            GroupId = groupId;
            Actor = actor;
            Reason = reason;
            Now = now;
            Result = result;
            CancellationToken = cancellationToken;
        }

        public ExperimentDocument Experiment { get; }

        public string GroupId { get; }

        public string Actor { get; }

        public string Reason { get; }

        public DateTime Now { get; }

        public ExperimentOperationResult Result { get; }

        public CancellationToken CancellationToken { get; }
    }
}
