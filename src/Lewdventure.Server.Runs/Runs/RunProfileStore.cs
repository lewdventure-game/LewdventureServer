using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Runs
{
    internal sealed class RunProfileStore : IRunProfileAccess
    {
        private readonly IConfigDistributor _configDistributor;
        private readonly PlayerProfileService _playerProfileService;
        private readonly PlayerRewardService _playerRewardService;
        private readonly string _userId;

        public RunProfileStore(string userId, IConfigDistributor configDistributor, PlayerProfileService playerProfileService, PlayerRewardService playerRewardService)
        {
            _userId = userId;
            _configDistributor = configDistributor;
            _playerProfileService = playerProfileService;
            _playerRewardService = playerRewardService;
        }

        public async Task<PlayerProfileDocument> GetAsync(CancellationToken cancellationToken)
        {
            return await _playerProfileService.GetOrCreateAsync(_userId, _configDistributor, cancellationToken);
        }

        public async Task<PlayerUpdateResult> GrantAsync(IReadOnlyList<BattleReward> rewards, string source, string stepKey, CancellationToken cancellationToken)
        {
            return await _playerRewardService.GrantAsync(_userId, rewards, source, stepKey, _configDistributor, cancellationToken);
        }
    }
}
