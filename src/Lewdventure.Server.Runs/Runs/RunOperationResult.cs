using Server.Infrastructure.Mongo.Runs;

namespace Server.Runs
{
    internal sealed class RunOperationResult
    {
        public RunOperationResult(RunDocument? run, RunStepOutcome? step, bool conflict, List<string> errors)
            : this(run, step, conflict, errors, false)
        {
        }

        public RunOperationResult(RunDocument? run, RunStepOutcome? step, bool conflict, List<string> errors, bool replayed)
        {
            Run = run;
            Step = step;
            Conflict = conflict;
            Errors = errors;
            Replayed = replayed;
        }

        public RunDocument? Run { get; }

        public RunStepOutcome? Step { get; }

        public bool Conflict { get; }

        public List<string> Errors { get; }

        public bool Replayed { get; }

        public bool Succeeded => Conflict == false && Errors.Count == 0;
    }
}
