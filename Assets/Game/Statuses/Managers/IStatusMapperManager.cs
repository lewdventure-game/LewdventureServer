using Core.Collections;

namespace Server.Statuses
{
    internal interface IStatusMapperManager : IManager<IStatusMapper>
    {
        public bool TryGet(int statusId, out IStatusMapper mapper);
    }
}
