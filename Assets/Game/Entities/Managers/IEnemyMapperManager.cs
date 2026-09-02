using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal interface IEnemyMapperManager : IManager<IEnemyMapper>
    {
        public bool TryGet(int enemyId, [MaybeNullWhen(false)] out IEnemyMapper enemyMapper);
    }
}
