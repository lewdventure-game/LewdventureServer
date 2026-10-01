using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    internal sealed class MasteryMapperManager : BaseManager<IMasteryMapper>, IMasteryMapperManager
    {
        public bool TryGet(int masteryId, int masteryLevel, [MaybeNullWhen(false)] out IMasteryMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != masteryId || currentMapper.MasteryLevel != masteryLevel)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }

        public int GetMaxMasteryLevel(int masteryId)
        {
            var maxLevel = 0;

            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != masteryId || currentMapper.MasteryLevel <= maxLevel)
                    continue;

                maxLevel = currentMapper.MasteryLevel;
            }

            return maxLevel;
        }
    }
}
