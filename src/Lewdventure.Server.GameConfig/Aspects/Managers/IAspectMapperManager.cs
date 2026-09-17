using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Aspects
{
    internal interface IAspectMapperManager : IManager<IAspectMapper>
    {
        public bool TryGet(int aspectId, [MaybeNullWhen(false)] out IAspectMapper mapper);
    }
}
