using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal interface IMasteryMapperManager : IManager<IMasteryMapper>
    {
        public bool TryGet(int masteryId, int masteryLevel, [MaybeNullWhen(false)] out IMasteryMapper mapper);
    }
}
