using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Equipments
{
    internal sealed class EquipmentMapperManager : BaseManager<IEquipmentMapper>, IEquipmentMapperManager
    {
        public bool TryGet(int equipmentId, [MaybeNullWhen(false)] out IEquipmentMapper equipmentMapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (mapper.Id != equipmentId)
                    continue;

                equipmentMapper = mapper;

                return true;
            }

            equipmentMapper = null;

            return false;
        }
    }
}
