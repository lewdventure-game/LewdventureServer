using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Perks
{
    public interface IPerkMapperManager : IManager<IPerkMapper>
    {
        public bool TryGet(int perkId, [MaybeNullWhen(false)] out IPerkMapper mapper);
    }
}
