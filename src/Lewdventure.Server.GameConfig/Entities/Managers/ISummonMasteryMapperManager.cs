using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface ISummonMasteryMapperManager : IManager<ISummonMasteryMapper>
    {
        public bool TryGet(int masteryId, int masteryLevel, [MaybeNullWhen(false)] out ISummonMasteryMapper mapper);

        public int GetMaxMasteryLevel(int masteryId);
    }
}
