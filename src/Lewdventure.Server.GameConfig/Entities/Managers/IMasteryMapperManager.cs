using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface IMasteryMapperManager : IManager<IMasteryMapper>
    {
        public bool TryGet(int masteryId, int masteryLevel, [MaybeNullWhen(false)] out IMasteryMapper mapper);
    }
}
