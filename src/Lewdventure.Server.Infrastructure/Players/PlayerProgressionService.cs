using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerProgressionService
    {
        private const int MaxAttempts = 3;
        private const string SummonLevelAction = "summon-level";
        private const string SummonMasteryAction = "summon-mastery";
        private const string EquipmentLevelAction = "equipment-level";
        private const string SpendEntry = "spend";
        private const string ProgressEntry = "progress";

        private readonly EquipmentProgressionRules _equipmentProgressionRules;
        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerProgressionService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly SummonProgressionRules _summonProgressionRules;
        private readonly TimeProvider _timeProvider;

        public PlayerProgressionService(
            EquipmentProgressionRules equipmentProgressionRules,
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerProgressionService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            SummonProgressionRules summonProgressionRules,
            TimeProvider timeProvider)
        {
            _equipmentProgressionRules = equipmentProgressionRules;
            _idempotencyRepository = idempotencyRepository;
            _logger = logger;
            _playerLedgerRepository = playerLedgerRepository;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _summonProgressionRules = summonProgressionRules;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerUpdateResult> UpgradeSummonLevelAsync(
            string userId,
            int summonId,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (configDistributor.Summons.TryGet(summonId, out var summonConfig) == false)
                return Failed($"Summon {summonId} is missing in configs.");

            if (await IsAlreadyAppliedAsync(userId, requestId, SummonLevelAction, cancellationToken))
                return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
                var summon = FindSummon(profile, summonId);

                if (summon == null)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Summon {summonId} is not owned.");
                }

                if (_summonProgressionRules.TryResolveLevelStep(summonConfig, summon.Level, summon.MasteryLevel, configDistributor, out var costs, out var error) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(error);
                }

                if (TrySpend(profile, costs, out var entries, out var spendError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(spendError);
                }

                summon.Level += 1;
                entries.Add(new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "summon:" + summonId + ":level", Amount = summon.Level });

                var saved = await SaveAsync(userId, profile, entries, SummonLevelAction, requestId, cancellationToken);

                if (saved)
                    return new PlayerUpdateResult(profile, false, new List<string>());
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        public async Task<PlayerUpdateResult> UpgradeSummonMasteryAsync(
            string userId,
            int summonId,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (configDistributor.Summons.TryGet(summonId, out var summonConfig) == false)
                return Failed($"Summon {summonId} is missing in configs.");

            if (await IsAlreadyAppliedAsync(userId, requestId, SummonMasteryAction, cancellationToken))
                return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
                var summon = FindSummon(profile, summonId);

                if (summon == null)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Summon {summonId} is not owned.");
                }

                if (_summonProgressionRules.TryResolveMasteryStep(summonConfig, summon.MasteryLevel, configDistributor, out var copies, out var error) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(error);
                }

                if (summon.Copies < copies)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Summon {summonId} needs {copies} copies, has {summon.Copies}.");
                }

                summon.Copies -= copies;
                summon.MasteryLevel += 1;

                var entries = new List<PlayerLedgerEntryDocument>
                {
                    new PlayerLedgerEntryDocument { Type = SpendEntry, Key = "summon:" + summonId + ":copies", Amount = -copies },
                    new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "summon:" + summonId + ":mastery", Amount = summon.MasteryLevel },
                };
                var saved = await SaveAsync(userId, profile, entries, SummonMasteryAction, requestId, cancellationToken);

                if (saved)
                    return new PlayerUpdateResult(profile, false, new List<string>());
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        public async Task<PlayerUpdateResult> UpgradeEquipmentLevelAsync(
            string userId,
            string instanceId,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (await IsAlreadyAppliedAsync(userId, requestId, EquipmentLevelAction, cancellationToken))
                return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
                var instance = FindEquipment(profile, instanceId);

                if (instance == null)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Equipment {instanceId} is not owned.");
                }

                if (configDistributor.Equipments.TryGet(instance.ConfigId, out var equipmentConfig) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Equipment config {instance.ConfigId} is missing.");
                }

                if (_equipmentProgressionRules.TryResolveLevelStep(equipmentConfig, instance.Level, out var costs, out var error) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(error);
                }

                if (TrySpend(profile, costs, out var entries, out var spendError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(spendError);
                }

                for (int i = 0; i < costs.Count; i++)
                    instance.ExpSpent += costs[i].Amount;

                instance.Level += 1;
                entries.Add(new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "equipment:" + instanceId + ":level", Amount = instance.Level });

                var saved = await SaveAsync(userId, profile, entries, EquipmentLevelAction, requestId, cancellationToken);

                if (saved)
                    return new PlayerUpdateResult(profile, false, new List<string>());
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        private bool TrySpend(PlayerProfileDocument profile, List<ResourceCost> costs, out List<PlayerLedgerEntryDocument> entries, out string error)
        {
            entries = new List<PlayerLedgerEntryDocument>(costs.Count);
            error = string.Empty;

            for (int i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];

                profile.Resources.TryGetValue(cost.Key, out var available);

                if (available < cost.Amount)
                {
                    error = $"Not enough {cost.Key}: need {cost.Amount}, have {available}.";

                    return false;
                }
            }

            for (int i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];

                profile.Resources[cost.Key] = profile.Resources[cost.Key] - cost.Amount;
                entries.Add(new PlayerLedgerEntryDocument { Type = SpendEntry, Key = cost.Key, Amount = -cost.Amount });
            }

            return true;
        }

        private async Task<bool> SaveAsync(
            string userId,
            PlayerProfileDocument profile,
            List<PlayerLedgerEntryDocument> entries,
            string action,
            string requestId,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expectedRev = profile.Rev;

            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                return false;

            await _playerLedgerRepository.AppendAsync(new PlayerLedgerDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Action = action,
                Source = action,
                RequestId = requestId,
                Rev = profile.Rev,
                CreatedAt = now,
                UpdatedAt = now,
                Entries = entries,
            }, cancellationToken);

            if (string.IsNullOrEmpty(requestId) == false)
                await _idempotencyRepository.CompleteAsync(userId, requestId, profile.Rev, now, cancellationToken);

            _logger.LogInformation("[Player] progression applied userId = {UserId} action = {Action} rev = {Rev}", userId, action, profile.Rev);

            return true;
        }

        private async Task<bool> IsAlreadyAppliedAsync(string userId, string requestId, string action, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return false;

            var existing = await _idempotencyRepository.GetAsync(userId, requestId, cancellationToken);

            if (existing != null)
                return true;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var reserved = new IdempotencyDocument
            {
                Id = _idempotencyRepository.CreateKey(userId, requestId),
                UserId = userId,
                RequestId = requestId,
                Action = action,
                CreatedAt = now,
                UpdatedAt = now,
            };

            return await _idempotencyRepository.TryReserveAsync(reserved, cancellationToken) == false;
        }

        private async Task ReleaseAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            await _idempotencyRepository.ReleaseAsync(userId, requestId, cancellationToken);
        }

        private PlayerEquipmentDocument? FindEquipment(PlayerProfileDocument profile, string instanceId)
        {
            for (int i = 0; i < profile.Equipment.Count; i++)
            {
                if (string.Equals(profile.Equipment[i].InstanceId, instanceId, StringComparison.Ordinal))
                    return profile.Equipment[i];
            }

            return null;
        }

        private PlayerSummonDocument? FindSummon(PlayerProfileDocument profile, int summonId)
        {
            for (int i = 0; i < profile.Summons.Count; i++)
            {
                if (profile.Summons[i].ConfigId == summonId)
                    return profile.Summons[i];
            }

            return null;
        }

        private PlayerUpdateResult Failed(string error)
        {
            return new PlayerUpdateResult(null, false, new List<string> { error });
        }

        private PlayerUpdateResult Conflict()
        {
            return new PlayerUpdateResult(null, true, new List<string>());
        }
    }
}
