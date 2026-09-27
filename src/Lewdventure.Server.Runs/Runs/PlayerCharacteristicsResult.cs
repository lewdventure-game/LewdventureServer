using Server.Battles;

namespace Server.Runs
{
    internal sealed class PlayerCharacteristicsResult
    {
        public PlayerCharacteristicsResult(ICharacteristicState? characteristics, string error)
        {
            Characteristics = characteristics;
            Error = error;
        }

        public ICharacteristicState? Characteristics { get; }

        public string Error { get; }

        public bool Succeeded => Characteristics != null;
    }
}
