using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Statuses
{
    public interface IStatusMapperManager : IManager<IStatusMapper>
    {
        public bool TryGet(int statusId, [MaybeNullWhen(false)] out IStatusMapper mapper);
    }
}
