using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Equipments
{
    internal interface IEquipmentMapperManager : IManager<IEquipmentMapper>
    {
        public bool TryGet(int equipmentId, [MaybeNullWhen(false)] out IEquipmentMapper equipmentMapper);
    }
}
