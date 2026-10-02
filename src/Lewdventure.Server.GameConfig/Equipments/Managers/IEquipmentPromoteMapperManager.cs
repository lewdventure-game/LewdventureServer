using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Equipments
{
    public interface IEquipmentPromoteMapperManager : IManager<IEquipmentPromoteMapper>
    {
        public bool TryGet(int promoteId, int level, [MaybeNullWhen(false)] out IEquipmentPromoteMapper mapper);

        public int GetMaxLevel(int promoteId);
    }
}
