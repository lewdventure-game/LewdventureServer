using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal interface ISummonLevelMapperManager : IManager<ISummonLevelMapper>
    {
        public bool TryGet(int patternId, int level, [MaybeNullWhen(false)] out ISummonLevelMapper mapper);
    }
}
