namespace Server.Admin.Backend.Models
{
    public sealed class BalanceLayerModel
    {
        public string Name { get; set; } = string.Empty;

        public PlayerCharacteristicsModel Characteristics { get; set; } = new();
    }
}
