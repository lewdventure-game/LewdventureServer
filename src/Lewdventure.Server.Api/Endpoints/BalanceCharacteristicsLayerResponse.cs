namespace Server.Api.Endpoints
{
    internal sealed class BalanceCharacteristicsLayerResponse
    {
        public string Name { get; set; } = string.Empty;

        public PlayerCharacteristicsResponse Characteristics { get; set; } = new();
    }
}
