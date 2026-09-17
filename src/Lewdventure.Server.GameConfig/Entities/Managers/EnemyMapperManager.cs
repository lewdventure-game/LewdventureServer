using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Entities
{
    internal sealed class EnemyMapperManager : BaseManager<IEnemyMapper>, IEnemyMapperManager
    {
        public bool TryGet(int enemyId, [MaybeNullWhen(false)] out IEnemyMapper enemyMapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (mapper.Id != enemyId)
                    continue;

                enemyMapper = mapper;

                return true;
            }

            enemyMapper = null;

            return false;
        }
    }
}
