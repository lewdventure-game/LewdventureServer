using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Perks
{
    public interface IPerkGroupMapperManager : IManager<IPerkGroupMapper>
    {
        public bool TryGet(int perkGroupId, [MaybeNullWhen(false)] out IPerkGroupMapper mapper);
    }
}
