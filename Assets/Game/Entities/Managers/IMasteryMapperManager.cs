using Core.Collections;

namespace Server.Entities
{
    internal interface IMasteryMapperManager : IManager<IMasteryMapper>
    {
        public bool TryGet(int masteryId, int masteryLevel, out IMasteryMapper mapper);
    }
}
