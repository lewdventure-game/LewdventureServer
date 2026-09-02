using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Perks
{
    internal sealed class PerkGroupMapperManager : BaseManager<IPerkGroupMapper>, IPerkGroupMapperManager
    {
        public bool TryGet(int perkGroupId, [MaybeNullWhen(false)] out IPerkGroupMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var entry = Collection[i];

                if (entry.Id != perkGroupId)
                    continue;

                mapper = entry;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
