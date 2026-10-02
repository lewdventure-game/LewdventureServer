using Server.Configs;

namespace Server.Equipments
{
    public interface IEquipmentPromoteMapper : IConfigMapper
    {
        public int Id { get; }

        public int Level { get; }

        public string[] ResourceTypes { get; }

        public string[] ResourceIds { get; }

        public int[] ResourceValues { get; }
    }
}
