using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentOperationResult
    {
        public ExperimentOperationStatus Status { get; set; } = ExperimentOperationStatus.Unknown;

        public ExperimentDocument? Experiment { get; set; }

        public List<string> Errors { get; } = new();

        public List<string> Warnings { get; } = new();

        public bool Succeeded => Status == ExperimentOperationStatus.Ok;
    }
}
