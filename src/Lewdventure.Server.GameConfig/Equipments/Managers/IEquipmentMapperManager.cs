using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Equipments
{
    public interface IEquipmentMapperManager : IManager<IEquipmentMapper>
    {
        public bool TryGet(int equipmentId, [MaybeNullWhen(false)] out IEquipmentMapper equipmentMapper);
    }
}
