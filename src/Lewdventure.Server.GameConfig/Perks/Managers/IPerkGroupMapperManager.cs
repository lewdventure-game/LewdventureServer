using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Perks
{
    public interface IPerkGroupMapperManager : IManager<IPerkGroupMapper>
    {
        public bool TryGet(int perkGroupId, [MaybeNullWhen(false)] out IPerkGroupMapper mapper);
    }
}
