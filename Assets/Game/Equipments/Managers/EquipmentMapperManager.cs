using System.Diagnostics.CodeAnalysis;
using Core.Collections;
using System.Globalization;

namespace Server.Equipments
{
    internal sealed class EquipmentMapperManager : BaseManager<IEquipmentMapper>, IEquipmentMapperManager
    {
        public bool TryGet(int equipmentId, [MaybeNullWhen(false)] out IEquipmentMapper equipmentMapper)
        {
            var equipmentIdText = equipmentId.ToString(CultureInfo.InvariantCulture);

            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (string.Equals(mapper.Id, equipmentIdText, StringComparison.Ordinal) == false)
                    continue;

                equipmentMapper = mapper;

                return true;
            }

            equipmentMapper = null;

            return false;
        }
    }
}
