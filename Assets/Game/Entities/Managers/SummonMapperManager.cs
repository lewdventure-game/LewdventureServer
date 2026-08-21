using Core.Collections;

namespace Server.Entities
{
    internal sealed class SummonMapperManager : BaseManager<ISummonMapper>, ISummonMapperManager
    {
        public bool TryGet(int summonId, out ISummonMapper summonMapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (mapper.Id != summonId)
                    continue;

                summonMapper = mapper;

                return true;
            }

            summonMapper = null;

            return false;
        }
    }
}
