using Core.Collections;

namespace Server.Aspects
{
    internal interface IAspectMapperManager : IManager<IAspectMapper>
    {
        public bool TryGet(int aspectId, out IAspectMapper mapper);
    }
}
