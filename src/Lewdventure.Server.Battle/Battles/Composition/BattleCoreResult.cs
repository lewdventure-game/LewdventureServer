namespace Server.Battles
{
    public sealed class BattleCoreResult
    {
        public BattleCoreResult(IBattleCore? battleCore, string configVersion, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            BattleCore = battleCore;
            ConfigVersion = configVersion;
            Errors = errors;
            Warnings = warnings;
        }

        public IBattleCore? BattleCore { get; }

        public string ConfigVersion { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        public bool Succeeded => BattleCore != null;
    }
}
