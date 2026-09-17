namespace Server.GameConfigs
{
    internal sealed class GameConfigBuildResult
    {
        public GameConfigBuildResult(GameConfigSet? configSet, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            ConfigSet = configSet;
            Errors = errors;
            Warnings = warnings;
        }

        public GameConfigSet? ConfigSet { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        public bool Succeeded => ConfigSet != null && Errors.Count == 0;
    }
}
