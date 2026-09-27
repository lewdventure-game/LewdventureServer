using Server.Infrastructure.Mongo.Runs;

namespace Server.Runs
{
    internal sealed class RunStageRoll
    {
        public RunStageRoll(List<RunStageDocument> stages, int rollIndex, List<string> errors)
        {
            Stages = stages;
            RollIndex = rollIndex;
            Errors = errors;
        }

        public List<RunStageDocument> Stages { get; }

        public int RollIndex { get; }

        public List<string> Errors { get; }
    }
}
