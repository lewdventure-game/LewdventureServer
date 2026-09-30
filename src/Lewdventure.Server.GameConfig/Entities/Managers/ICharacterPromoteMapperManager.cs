using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface ICharacterPromoteMapperManager : IManager<ICharacterPromoteMapper>
    {
        public bool TryGet(int promoteId, int promoteLevel, [MaybeNullWhen(false)] out ICharacterPromoteMapper mapper);

        public int GetMaxPromoteLevel(int promoteId);
    }
}
