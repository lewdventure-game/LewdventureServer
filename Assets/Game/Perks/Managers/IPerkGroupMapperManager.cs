using Core.Collections;

namespace Server.Perks
{
    internal interface IPerkGroupMapperManager : IManager<IPerkGroupMapper>
    {
        public bool TryGet(int perkGroupId, out IPerkGroupMapper mapper);
    }
}
