namespace Server.Runs
{
    internal sealed class RunEngineStep
    {
        public RunEngineStep(RunEngineStepKind kind, RunStepOutcome? outcome, string error, RunBattleFinish? battleFinish)
        {
            Kind = kind;
            Outcome = outcome;
            Error = error;
            BattleFinish = battleFinish;
        }

        public RunEngineStepKind Kind { get; }

        public RunStepOutcome? Outcome { get; }

        public string Error { get; }

        public RunBattleFinish? BattleFinish { get; }
    }
}
