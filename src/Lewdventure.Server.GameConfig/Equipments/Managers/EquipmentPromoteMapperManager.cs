using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Equipments
{
    internal sealed class EquipmentPromoteMapperManager : BaseManager<IEquipmentPromoteMapper>, IEquipmentPromoteMapperManager
    {
        public bool TryGet(int promoteId, int level, [MaybeNullWhen(false)] out IEquipmentPromoteMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var candidate = Collection[i];

                if (candidate.Id != promoteId || candidate.Level != level)
                    continue;

                mapper = candidate;

                return true;
            }

            mapper = null;

            return false;
        }

        public int GetMaxLevel(int promoteId)
        {
            var maxLevel = 1;

            for (int i = 0; i < Collection.Count; i++)
            {
                var candidate = Collection[i];

                if (candidate.Id != promoteId || candidate.Level <= maxLevel)
                    continue;

                maxLevel = candidate.Level;
            }

            return maxLevel;
        }
    }
}
