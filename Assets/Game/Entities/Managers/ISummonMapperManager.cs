using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal interface ISummonMapperManager : IManager<ISummonMapper>
    {
        public bool TryGet(int summonId, [MaybeNullWhen(false)] out ISummonMapper summonMapper);
    }
}
