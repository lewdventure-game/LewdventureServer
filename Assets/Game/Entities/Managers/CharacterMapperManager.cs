using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal sealed class CharacterMapperManager : BaseManager<ICharacterMapper>, ICharacterMapperManager
    {
        public bool TryGet(int characterId, [MaybeNullWhen(false)] out ICharacterMapper characterMapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (mapper.Id != characterId)
                    continue;

                characterMapper = mapper;

                return true;
            }

            characterMapper = null;

            return false;
        }
    }
}
