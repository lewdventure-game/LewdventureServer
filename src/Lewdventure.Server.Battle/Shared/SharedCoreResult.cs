namespace Server.Shared
{
    public sealed class SharedCoreResult
    {
        public SharedCoreResult(ISharedCore? sharedCore, string configVersion, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            SharedCore = sharedCore;
            ConfigVersion = configVersion;
            Errors = errors;
            Warnings = warnings;
        }

        public ISharedCore? SharedCore { get; }

        public string ConfigVersion { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        public bool Succeeded => SharedCore != null;
    }
}
