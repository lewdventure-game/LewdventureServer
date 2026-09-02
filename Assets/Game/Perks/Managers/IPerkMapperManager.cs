using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Perks
{
    internal interface IPerkMapperManager : IManager<IPerkMapper>
    {
        public bool TryGet(int perkId, [MaybeNullWhen(false)] out IPerkMapper mapper);
    }
}
