using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Runs;
using Server.Services;

namespace Server.Balance
{
    internal sealed class BalanceProfileAccess : IRunProfileAccess
    {
        private const string NothingChangedError = "Rewards changed nothing in the profile.";

        private readonly IConfigDistributor _configDistributor;
        private readonly DateTime _now;
        private readonly PlayerProfileDocument _profile;
        private readonly RewardApplier _rewardApplier;

        public BalanceProfileAccess(PlayerProfileDocument profile, IConfigDistributor configDistributor, RewardApplier rewardApplier, DateTime now)
        {
            _profile = profile;
            _configDistributor = configDistributor;
            _rewardApplier = rewardApplier;
            _now = now;
        }

        public Task<PlayerProfileDocument> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_profile);
        }

        public Task<PlayerUpdateResult> GrantAsync(IReadOnlyList<BattleReward> rewards, string source, string stepKey, CancellationToken cancellationToken)
        {
            var entries = _rewardApplier.Apply(_profile, rewards, _configDistributor, _now);

            if (entries.Count == 0)
                return Task.FromResult(new PlayerUpdateResult(_profile, false, new List<string> { NothingChangedError }));

            _profile.Rev += 1;

            return Task.FromResult(new PlayerUpdateResult(_profile, false, new List<string>()));
        }
    }
}
