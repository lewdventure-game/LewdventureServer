using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Perks
{
    internal sealed class PerkMapperManager : BaseManager<IPerkMapper>, IPerkMapperManager
    {
        public bool TryGet(int perkId, [MaybeNullWhen(false)] out IPerkMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var entry = Collection[i];

                if (entry.Id != perkId)
                    continue;

                mapper = entry;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
