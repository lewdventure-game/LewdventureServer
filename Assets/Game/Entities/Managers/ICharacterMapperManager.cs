using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal interface ICharacterMapperManager : IManager<ICharacterMapper>
    {
        public bool TryGet(int characterId, [MaybeNullWhen(false)] out ICharacterMapper characterMapper);
    }
}
