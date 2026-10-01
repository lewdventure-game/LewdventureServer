using Server.Equipments;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Players
{
    internal sealed class EquipmentMergeCandidate
    {
        public EquipmentMergeCandidate(PlayerEquipmentDocument instance, IEquipmentMapper mapper)
        {
            Instance = instance;
            Mapper = mapper;
        }

        public PlayerEquipmentDocument Instance { get; }

        public IEquipmentMapper Mapper { get; }

        public bool IsUsed { get; set; }
    }
}
