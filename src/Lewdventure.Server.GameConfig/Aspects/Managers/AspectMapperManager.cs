using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Aspects
{
    internal sealed class AspectMapperManager : BaseManager<IAspectMapper>, IAspectMapperManager
    {
        public bool TryGet(int aspectId, [MaybeNullWhen(false)] out IAspectMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != aspectId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
