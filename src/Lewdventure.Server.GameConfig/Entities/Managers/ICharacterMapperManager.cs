using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface ICharacterMapperManager : IManager<ICharacterMapper>
    {
        public bool TryGet(int characterId, [MaybeNullWhen(false)] out ICharacterMapper characterMapper);
    }
}
