using Core.Collections;

namespace Server.Equipments
{
    internal interface IEquipmentMapperManager : IManager<IEquipmentMapper>
    {
        public bool TryGet(int equipmentId, out IEquipmentMapper equipmentMapper);
    }
}
