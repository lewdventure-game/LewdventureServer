namespace Server.Runs
{
    internal enum RunEngineStepKind
    {
        Failed,
        Conflict,
        LevelEnd,
        Stage,
        BattlePending,
        PerksChosen,
        BattleLost,
    }
}
