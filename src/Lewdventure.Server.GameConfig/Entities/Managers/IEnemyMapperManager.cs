using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Entities
{
    public interface IEnemyMapperManager : IManager<IEnemyMapper>
    {
        public bool TryGet(int enemyId, [MaybeNullWhen(false)] out IEnemyMapper enemyMapper);
    }
}
