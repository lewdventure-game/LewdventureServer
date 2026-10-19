using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

namespace Server.Runs
{
    internal interface IRunProfileAccess
    {
        public Task<PlayerProfileDocument> GetAsync(CancellationToken cancellationToken);

        public Task<PlayerUpdateResult> GrantAsync(IReadOnlyList<BattleReward> rewards, string source, string stepKey, CancellationToken cancellationToken);
    }
}
