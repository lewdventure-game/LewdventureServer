using Core.Collections;

namespace Server.Entities
{
    internal interface ISummonLevelMapperManager : IManager<ISummonLevelMapper>
    {
        public bool TryGet(int patternId, int level, out ISummonLevelMapper mapper);
    }
}
