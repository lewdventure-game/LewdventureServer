using Core.Collections;

namespace Server.Entities
{
    internal interface IEnemyMapperManager : IManager<IEnemyMapper>
    {
        public bool TryGet(int enemyId, out IEnemyMapper enemyMapper);
    }
}
