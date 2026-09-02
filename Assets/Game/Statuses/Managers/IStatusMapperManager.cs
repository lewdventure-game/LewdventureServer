using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Statuses
{
    internal interface IStatusMapperManager : IManager<IStatusMapper>
    {
        public bool TryGet(int statusId, [MaybeNullWhen(false)] out IStatusMapper mapper);
    }
}
