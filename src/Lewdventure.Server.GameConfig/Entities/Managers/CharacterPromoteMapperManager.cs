using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    internal sealed class CharacterPromoteMapperManager : BaseManager<ICharacterPromoteMapper>, ICharacterPromoteMapperManager
    {
        public bool TryGet(int promoteId, int promoteLevel, [MaybeNullWhen(false)] out ICharacterPromoteMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != promoteId || currentMapper.PromoteLevel != promoteLevel)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }

        public int GetMaxPromoteLevel(int promoteId)
        {
            var maxLevel = 0;

            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != promoteId || currentMapper.PromoteLevel <= maxLevel)
                    continue;

                maxLevel = currentMapper.PromoteLevel;
            }

            return maxLevel;
        }
    }
}
