using Server.Configs;

namespace Server.Entities
{
    internal interface IMasteryMapper : IConfigMapper
    {
        public int Id { get; }

        public int MasteryLevel { get; }

        public int CopiesToUpgrade { get; }

        public int BonusId { get; }

        public string ArtName { get; }

        public float DamageMultiplier { get; }
    }
}
