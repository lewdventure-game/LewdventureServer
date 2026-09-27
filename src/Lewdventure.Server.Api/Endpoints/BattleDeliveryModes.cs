namespace Server.Api.Endpoints
{
    internal sealed class BattleDeliveryModes
    {
        internal const string Script = "script";
        internal const string Seed = "seed";

        public bool IsSeedOnly(string battleDelivery)
        {
            return string.Equals(battleDelivery, Seed, StringComparison.OrdinalIgnoreCase);
        }
    }
}
