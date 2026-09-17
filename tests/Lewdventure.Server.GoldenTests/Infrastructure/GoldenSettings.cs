namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenSettings
    {
        private const string UpdateVariable = "LEWD_GOLDEN_UPDATE";

        private readonly ulong[] _seeds = { 1UL, 42UL, 1337UL, 123456789UL, 18446744073709551615UL };

        public bool IsUpdateMode => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

        public IReadOnlyList<ulong> Seeds => _seeds;
    }
}
