using Core.Collections;

namespace Server.Statuses
{
    internal sealed class StatusMapperManager : BaseManager<IStatusMapper>, IStatusMapperManager
    {
        public bool TryGet(int statusId, out IStatusMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != statusId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
