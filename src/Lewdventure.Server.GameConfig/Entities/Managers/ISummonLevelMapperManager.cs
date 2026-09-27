using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface ISummonLevelMapperManager : IManager<ISummonLevelMapper>
    {
        public bool TryGet(int patternId, int level, [MaybeNullWhen(false)] out ISummonLevelMapper mapper);
    }
}
