using System.Globalization;
using Server.Battles;
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
        private const string SummonLevelResetAction = "summon-level-reset";
        private const string EquipmentLevelResetAction = "equipment-level-reset";
        private const string EquipmentMergeAction = "equipment-merge";
        private const string SummonLevelResetCoefficientConstant = "summon_level_reset_coeff";
        private const string SummonLevelResetResourceConstant = "summon_level_reset_resource";
        private const string EquipmentLevelDropProportionConstant = "equipment_lvl_drop_proportion";
        private const string EquipmentInstancePrefix = "eq_";
        private const string RefundEntry = "refund";
        private const string SpendEntry = "spend";
        private const string ProgressEntry = "progress";

        private readonly IBattleRewardParser _battleRewardParser;
        private readonly EquipmentMergeRules _equipmentMergeRules;
        private readonly EquipmentProgressionRules _equipmentProgressionRules;
        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerProgressionService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly SummonProgressionRules _summonProgressionRules;
        private readonly TimeProvider _timeProvider;

        public PlayerProgressionService(
            IBattleRewardParser battleRewardParser,
            EquipmentMergeRules equipmentMergeRules,
            EquipmentProgressionRules equipmentProgressionRules,
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerProgressionService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            SummonProgressionRules summonProgressionRules,
            TimeProvider timeProvider)
        {
            _battleRewardParser = battleRewardParser;
            _equipmentMergeRules = equipmentMergeRules;
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

        public async Task<PlayerUpdateResult> ResetSummonLevelAsync(
            string userId,
            int summonId,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (configDistributor.Summons.TryGet(summonId, out var summonConfig) == false)
                return Failed($"Summon {summonId} is missing in configs.");

            if (TryReadFloatConstant(configDistributor, SummonLevelResetCoefficientConstant, out var coefficient) == false)
                return Failed($"Constant {SummonLevelResetCoefficientConstant} is missing or unreadable.");

            if (TryReadResourceConstant(configDistributor, SummonLevelResetResourceConstant, out var resetCosts) == false)
                return Failed($"Constant {SummonLevelResetResourceConstant} is missing or unreadable.");

            if (await IsAlreadyAppliedAsync(userId, requestId, SummonLevelResetAction, cancellationToken))
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

                if (_summonProgressionRules.TryResolveLevelRefund(summonConfig, summon.Level, coefficient, configDistributor, out var refunds, out var refundError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(refundError);
                }

                if (TrySpend(profile, resetCosts, out var entries, out var spendError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(spendError);
                }

                var previousLevel = summon.Level;

                summon.Level = 1;
                Refund(profile, refunds, entries);
                entries.Add(new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "summon:" + summonId + ":level", Amount = summon.Level });

                var saved = await SaveAsync(userId, profile, entries, SummonLevelResetAction, requestId, cancellationToken);

                if (saved)
                {
                    _logger.LogInformation("[Player] summon {SummonId} level reset from {Level} userId = {UserId}", summonId, previousLevel, userId);

                    return new PlayerUpdateResult(profile, false, new List<string>());
                }
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        public async Task<PlayerUpdateResult> ResetEquipmentLevelAsync(
            string userId,
            string instanceId,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (TryReadFloatConstant(configDistributor, EquipmentLevelDropProportionConstant, out var dropProportion) == false)
                return Failed($"Constant {EquipmentLevelDropProportionConstant} is missing or unreadable.");

            if (await IsAlreadyAppliedAsync(userId, requestId, EquipmentLevelResetAction, cancellationToken))
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

                if (_equipmentProgressionRules.TryResolveLevelRefund(equipmentConfig, instance.Level, dropProportion, out var refunds, out var refundError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(refundError);
                }

                var previousLevel = instance.Level;
                var entries = new List<PlayerLedgerEntryDocument>();

                instance.Level = 1;
                instance.ExpSpent = 0;
                Refund(profile, refunds, entries);
                entries.Add(new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "equipment:" + instanceId + ":level", Amount = instance.Level });

                var saved = await SaveAsync(userId, profile, entries, EquipmentLevelResetAction, requestId, cancellationToken);

                if (saved)
                {
                    _logger.LogInformation("[Player] equipment {InstanceId} level reset from {Level} userId = {UserId}", instanceId, previousLevel, userId);

                    return new PlayerUpdateResult(profile, false, new List<string>());
                }
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        public async Task<PlayerUpdateResult> MergeEquipmentAsync(
            string userId,
            string instanceId,
            IReadOnlyList<string> paymentInstanceIds,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (await IsAlreadyAppliedAsync(userId, requestId, EquipmentMergeAction, cancellationToken))
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

                if (configDistributor.Equipments.TryGet(instance.ConfigId, out var sourceConfig) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Equipment config {instance.ConfigId} is missing.");
                }

                if (_equipmentMergeRules.TryResolveRequirements(sourceConfig, out var requirements, out var requirementsError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(requirementsError);
                }

                if (TryCollectMergeCandidates(profile, instanceId, paymentInstanceIds, configDistributor, out var candidates, out var candidatesError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(candidatesError);
                }

                if (_equipmentMergeRules.TryMatchPayment(sourceConfig, requirements, candidates, out var matchError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(matchError);
                }

                if (_equipmentMergeRules.TryResolveTarget(sourceConfig, configDistributor, out var targetConfig, out var targetError) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed(targetError);
                }

                if (int.TryParse(targetConfig.Id, out var targetConfigId) == false)
                {
                    await ReleaseAsync(userId, requestId, cancellationToken);

                    return Failed($"Equipment config id {targetConfig.Id} is not a number.");
                }

                _equipmentMergeRules.TryParseMergeNumber(targetConfig.MergeNumber, out var targetMergeNumber);

                var sourceSlot = FindEquippedSlot(profile, instanceId);
                var entries = new List<PlayerLedgerEntryDocument>
                {
                    new PlayerLedgerEntryDocument { Type = SpendEntry, Key = "equipment:" + instanceId, Amount = -1 },
                };

                RemoveEquipment(profile, instanceId);

                for (int i = 0; i < candidates.Count; i++)
                {
                    var payment = candidates[i].Instance;

                    entries.Add(new PlayerLedgerEntryDocument { Type = SpendEntry, Key = "equipment:" + payment.InstanceId, Amount = -1 });
                    RemoveEquipment(profile, payment.InstanceId);
                }

                var merged = new PlayerEquipmentDocument
                {
                    InstanceId = EquipmentInstancePrefix + Guid.NewGuid().ToString("N"),
                    ConfigId = targetConfigId,
                    Level = instance.Level,
                    MergeNumber = targetMergeNumber,
                    ObtainedAt = _timeProvider.GetUtcNow().UtcDateTime,
                };

                profile.Equipment.Add(merged);

                if (string.IsNullOrEmpty(sourceSlot) == false)
                    profile.Loadout.Equipment[sourceSlot] = merged.InstanceId;

                entries.Add(new PlayerLedgerEntryDocument { Type = ProgressEntry, Key = "equipment:" + merged.InstanceId + ":merge", Amount = targetMergeNumber });

                var saved = await SaveAsync(userId, profile, entries, EquipmentMergeAction, requestId, cancellationToken);

                if (saved)
                {
                    _logger.LogInformation(
                        "[Player] equipment {InstanceId} merged into {TargetId} level {Level} userId = {UserId}",
                        instanceId,
                        targetConfigId,
                        merged.Level,
                        userId);

                    return new PlayerUpdateResult(profile, false, new List<string>());
                }
            }

            await ReleaseAsync(userId, requestId, cancellationToken);

            return Conflict();
        }

        private bool TryCollectMergeCandidates(
            PlayerProfileDocument profile,
            string instanceId,
            IReadOnlyList<string> paymentInstanceIds,
            IConfigDistributor configDistributor,
            out List<EquipmentMergeCandidate> candidates,
            out string error)
        {
            candidates = new List<EquipmentMergeCandidate>();
            error = string.Empty;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < paymentInstanceIds.Count; i++)
            {
                var paymentId = paymentInstanceIds[i];

                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    error = "Payment instance id is empty.";

                    return false;
                }

                if (string.Equals(paymentId, instanceId, StringComparison.Ordinal))
                {
                    error = "Transformed equipment cannot pay for itself.";

                    return false;
                }

                if (seen.Add(paymentId) == false)
                {
                    error = $"Payment {paymentId} is listed twice.";

                    return false;
                }

                var payment = FindEquipment(profile, paymentId);

                if (payment == null)
                {
                    error = $"Equipment {paymentId} is not owned.";

                    return false;
                }

                if (configDistributor.Equipments.TryGet(payment.ConfigId, out var paymentConfig) == false)
                {
                    error = $"Equipment config {payment.ConfigId} is missing.";

                    return false;
                }

                candidates.Add(new EquipmentMergeCandidate(payment, paymentConfig));
            }

            return true;
        }

        private string FindEquippedSlot(PlayerProfileDocument profile, string instanceId)
        {
            foreach (var pair in profile.Loadout.Equipment)
            {
                if (string.Equals(pair.Value, instanceId, StringComparison.Ordinal))
                    return pair.Key;
            }

            return string.Empty;
        }

        private void RemoveEquipment(PlayerProfileDocument profile, string instanceId)
        {
            for (int i = profile.Equipment.Count - 1; 0 <= i; i--)
            {
                if (string.Equals(profile.Equipment[i].InstanceId, instanceId, StringComparison.Ordinal))
                    profile.Equipment.RemoveAt(i);
            }

            var slot = FindEquippedSlot(profile, instanceId);

            if (string.IsNullOrEmpty(slot) == false)
                profile.Loadout.Equipment.Remove(slot);
        }

        private void Refund(PlayerProfileDocument profile, List<ResourceCost> refunds, List<PlayerLedgerEntryDocument> entries)
        {
            for (int i = 0; i < refunds.Count; i++)
            {
                var refund = refunds[i];

                profile.Resources.TryGetValue(refund.Key, out var current);
                profile.Resources[refund.Key] = current + refund.Amount;
                entries.Add(new PlayerLedgerEntryDocument { Type = RefundEntry, Key = refund.Key, Amount = refund.Amount });
            }
        }

        private bool TryReadFloatConstant(IConfigDistributor configDistributor, string constantKey, out float value)
        {
            value = 0f;

            if (configDistributor.Constants.TryGet(constantKey, out var constant) == false)
                return false;

            return float.TryParse(constant.ConstantValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private bool TryReadResourceConstant(IConfigDistributor configDistributor, string constantKey, out List<ResourceCost> costs)
        {
            costs = new List<ResourceCost>();

            if (configDistributor.Constants.TryGet(constantKey, out var constant) == false)
                return false;

            var rewards = _battleRewardParser.Parse(constant.ConstantValue);

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];

                if (reward.Type != BattleRewardType.Resource || reward.HasStringRewardKey == false || reward.Count <= 0)
                    continue;

                costs.Add(new ResourceCost(reward.RewardKey, reward.Count));
            }

            return 0 < costs.Count;
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
