using Server.Battles;

namespace Server.Balance
{
    internal sealed class BalanceCharacteristicsLayer
    {
        public string Name { get; set; } = string.Empty;

        public ICharacteristicState Characteristics { get; set; } = null!;
    }
}
