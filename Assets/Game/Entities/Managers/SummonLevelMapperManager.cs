using Core.Collections;

namespace Server.Entities
{
    internal sealed class SummonLevelMapperManager : BaseManager<ISummonLevelMapper>, ISummonLevelMapperManager
    {
        public bool TryGet(int patternId, int level, out ISummonLevelMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.PatternId != patternId || currentMapper.Level != level)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
