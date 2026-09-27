using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerRewardService
    {
        private const int MaxAttempts = 3;

        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerRewardService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly RewardApplier _rewardApplier;
        private readonly TimeProvider _timeProvider;

        public PlayerRewardService(
            IBattleRewardParser battleRewardParser,
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerRewardService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            RewardApplier rewardApplier,
            TimeProvider timeProvider)
        {
            _battleRewardParser = battleRewardParser;
            _idempotencyRepository = idempotencyRepository;
            _logger = logger;
            _playerLedgerRepository = playerLedgerRepository;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _rewardApplier = rewardApplier;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerUpdateResult> GrantAsync(
            string userId,
            string rewards,
            string source,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            var parsed = _battleRewardParser.Parse(rewards);

            if (parsed.Count == 0)
                return new PlayerUpdateResult(null, false, new List<string> { "Rewards string has no valid entries." });

            return await GrantAsync(userId, parsed, source, requestId, configDistributor, cancellationToken);
        }

        public async Task<PlayerUpdateResult> GrantAsync(
            string userId,
            IReadOnlyList<BattleReward> rewards,
            string source,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId) == false)
            {
                var applied = await _idempotencyRepository.GetAsync(userId, requestId, cancellationToken);

                if (applied != null && 0 < applied.ResultRev)
                    return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());

                if (applied == null && await ReserveAsync(userId, requestId, source, cancellationToken) == false)
                    return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
                var expectedRev = profile.Rev;
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var entries = _rewardApplier.Apply(profile, rewards, configDistributor, now);

                if (entries.Count == 0)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return new PlayerUpdateResult(profile, false, new List<string> { "Rewards changed nothing in the profile." });
                }

                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = now;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                    continue;

                await _playerLedgerRepository.AppendAsync(new PlayerLedgerDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    Action = "grant",
                    Source = source,
                    RequestId = requestId,
                    Rev = profile.Rev,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Entries = entries,
                }, cancellationToken);

                await CompleteAsync(userId, requestId, profile.Rev, now, cancellationToken);
                _logger.LogInformation("[Player] rewards granted userId = {UserId} source = {Source} entries = {Entries} rev = {Rev}", userId, source, entries.Count, profile.Rev);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            await ReleaseAsync(userId, requestId, cancellationToken);
            _logger.LogWarning("[Player] rewards conflict userId = {UserId} source = {Source}", userId, source);

            return new PlayerUpdateResult(null, true, new List<string>());
        }

        private async Task<bool> ReserveAsync(string userId, string requestId, string source, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return await _idempotencyRepository.TryReserveAsync(new IdempotencyDocument
            {
                Id = _idempotencyRepository.CreateKey(userId, requestId),
                UserId = userId,
                RequestId = requestId,
                Action = source,
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);
        }

        private async Task CompleteAsync(string userId, string requestId, long resultRev, DateTime now, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            await _idempotencyRepository.CompleteAsync(userId, requestId, resultRev, now, cancellationToken);
        }

        private async Task ReleaseAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            await _idempotencyRepository.ReleaseAsync(userId, requestId, cancellationToken);
        }
    }
}
