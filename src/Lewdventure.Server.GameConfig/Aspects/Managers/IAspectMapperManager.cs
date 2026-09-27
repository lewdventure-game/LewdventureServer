using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Aspects
{
    public interface IAspectMapperManager : IManager<IAspectMapper>
    {
        public bool TryGet(int aspectId, [MaybeNullWhen(false)] out IAspectMapper mapper);
    }
}
