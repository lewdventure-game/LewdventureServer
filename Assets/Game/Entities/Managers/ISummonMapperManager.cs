using Core.Collections;

namespace Server.Entities
{
    internal interface ISummonMapperManager : IManager<ISummonMapper>
    {
        public bool TryGet(int summonId, out ISummonMapper summonMapper);
    }
}
