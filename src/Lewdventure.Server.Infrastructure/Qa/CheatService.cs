using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Infrastructure.Qa
{
    internal sealed class CheatService
    {
        private const int MaxAttempts = 3;
        private const string CheatAction = "cheat";

        private readonly CheatPresetBuilder _cheatPresetBuilder;
        private readonly CheatProfileEditor _cheatProfileEditor;
        private readonly ILogger<CheatService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerRewardService _playerRewardService;
        private readonly TimeProvider _timeProvider;

        public CheatService(
            CheatPresetBuilder cheatPresetBuilder,
            CheatProfileEditor cheatProfileEditor,
            ILogger<CheatService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            PlayerRewardService playerRewardService,
            TimeProvider timeProvider)
        {
            _cheatPresetBuilder = cheatPresetBuilder;
            _cheatProfileEditor = cheatProfileEditor;
            _logger = logger;
            _playerLedgerRepository = playerLedgerRepository;
            _playerProfileRepository = playerProfileRepository;
            _playerRewardService = playerRewardService;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerUpdateResult> GrantPresetAsync(
            string userId,
            string preset,
            int amount,
            string actor,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            var profile = await _playerProfileRepository.GetAsync(userId, cancellationToken);

            if (profile == null)
                return Failed(CreateMissingProfileError(userId));

            var rewards = new List<BattleReward>();

            if (_cheatPresetBuilder.TryBuild(preset, amount, profile, configDistributor, rewards, out var error) == false)
                return Failed(error);

            if (rewards.Count == 0)
                return new PlayerUpdateResult(profile, false, new List<string>());

            var source = actor + ":" + CheatAction + ":" + preset;
            var result = await _playerRewardService.GrantAsync(userId, rewards, source, string.Empty, configDistributor, cancellationToken);

            if (result.Succeeded)
                _logger.LogInformation("[Cheat] preset granted userId = {UserId} actor = {Actor} preset = {Preset} rewards = {Rewards}", userId, actor, preset, rewards.Count);

            return result;
        }

        public async Task<PlayerUpdateResult> SetResourceAsync(string userId, string key, long amount, string actor, CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "resource:" + key, (profile, now, entries) =>
            {
                _cheatProfileEditor.SetResource(profile, key, amount, entries, out var error);

                return error;
            }, cancellationToken);
        }

        public async Task<PlayerUpdateResult> SetFlagAsync(string userId, string key, int value, string actor, CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "flag:" + key, (profile, now, entries) =>
            {
                _cheatProfileEditor.SetFlag(profile, key, value, entries, out var error);

                return error;
            }, cancellationToken);
        }

        public async Task<PlayerUpdateResult> SetCharacterAsync(
            string userId,
            int characterId,
            int promoteLevel,
            string actor,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "character:" + characterId, (profile, now, entries) =>
            {
                _cheatProfileEditor.SetCharacterPromote(profile, characterId, promoteLevel, configDistributor, now, entries, out var error);

                return error;
            }, cancellationToken);
        }

        public async Task<PlayerUpdateResult> SetSummonAsync(
            string userId,
            int summonId,
            int? level,
            int? masteryLevel,
            int? skillLevel,
            string actor,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "summon:" + summonId, (profile, now, entries) =>
            {
                _cheatProfileEditor.SetSummon(profile, summonId, level, masteryLevel, skillLevel, configDistributor, now, entries, out var error);

                return error;
            }, cancellationToken);
        }

        public async Task<PlayerUpdateResult> SetEquipmentAsync(
            string userId,
            string instanceId,
            int level,
            string actor,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "equipment:" + instanceId, (profile, now, entries) =>
            {
                _cheatProfileEditor.SetEquipmentLevel(profile, instanceId, level, configDistributor, entries, out var error);

                return error;
            }, cancellationToken);
        }

        public async Task<PlayerUpdateResult> MaxOutAsync(string userId, string actor, IConfigDistributor configDistributor, CancellationToken cancellationToken)
        {
            return await EditAsync(userId, actor, "max", (profile, now, entries) =>
            {
                _cheatProfileEditor.MaxOut(profile, configDistributor, now, entries);

                return string.Empty;
            }, cancellationToken);
        }

        private async Task<PlayerUpdateResult> EditAsync(
            string userId,
            string actor,
            string operation,
            Func<PlayerProfileDocument, DateTime, List<PlayerLedgerEntryDocument>, string> edit,
            CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileRepository.GetAsync(userId, cancellationToken);

                if (profile == null)
                    return Failed(CreateMissingProfileError(userId));

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var expectedRev = profile.Rev;
                var entries = new List<PlayerLedgerEntryDocument>();
                var error = edit(profile, now, entries);

                if (error.Length != 0)
                    return Failed(error);

                if (entries.Count == 0)
                    return new PlayerUpdateResult(profile, false, new List<string>());

                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = now;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                    continue;

                await _playerLedgerRepository.AppendAsync(new PlayerLedgerDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    Action = CheatAction,
                    Source = actor + ":" + CheatAction + ":" + operation,
                    RequestId = string.Empty,
                    Rev = profile.Rev,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Entries = entries,
                }, cancellationToken);

                _logger.LogInformation("[Cheat] applied userId = {UserId} actor = {Actor} operation = {Operation} entries = {Entries} rev = {Rev}", userId, actor, operation, entries.Count, profile.Rev);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            _logger.LogWarning("[Cheat] conflict userId = {UserId} actor = {Actor} operation = {Operation}", userId, actor, operation);

            return new PlayerUpdateResult(null, true, new List<string>());
        }

        private string CreateMissingProfileError(string userId)
        {
            return $"Player {userId} has no profile yet: open the game with this account first.";
        }

        private PlayerUpdateResult Failed(string error)
        {
            return new PlayerUpdateResult(null, false, new List<string> { error });
        }
    }
}
