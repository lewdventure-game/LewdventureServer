using Core.Collections;

namespace Server.Entities
{
    internal interface ICharacterMapperManager : IManager<ICharacterMapper>
    {
        public bool TryGet(int characterId, out ICharacterMapper characterMapper);
    }
}
