using Core.Collections;

namespace Server.Perks
{
    internal interface IPerkMapperManager : IManager<IPerkMapper>
    {
        public bool TryGet(int perkId, out IPerkMapper mapper);
    }
}
