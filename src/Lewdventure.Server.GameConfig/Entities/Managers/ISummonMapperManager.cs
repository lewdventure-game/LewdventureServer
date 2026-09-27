using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface ISummonMapperManager : IManager<ISummonMapper>
    {
        public bool TryGet(int summonId, [MaybeNullWhen(false)] out ISummonMapper summonMapper);
    }
}
