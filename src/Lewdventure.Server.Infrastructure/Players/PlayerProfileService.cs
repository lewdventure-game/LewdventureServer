using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerProfileService
    {
        private const int MaxSummons = 3;
        private const string LoadoutAction = "loadout";

        private const string StartContentConstant = "start_content";

        private readonly EquipmentSlotReader _equipmentSlotReader = new();
        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerProfileService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly RewardApplier _rewardApplier;
        private readonly TimeProvider _timeProvider;

        public PlayerProfileService(
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerProfileService> logger,
            PlayerProfileRepository playerProfileRepository,
            RewardApplier rewardApplier,
            TimeProvider timeProvider)
        {
            _idempotencyRepository = idempotencyRepository;
            _logger = logger;
            _playerProfileRepository = playerProfileRepository;
            _rewardApplier = rewardApplier;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerProfileDocument> GetOrCreateAsync(string userId, IConfigDistributor configDistributor, CancellationToken cancellationToken)
        {
            var profile = await GetOrCreateAsync(userId, cancellationToken);

            return await EnsureStartContentAsync(profile, configDistributor, cancellationToken);
        }

        public async Task<PlayerProfileDocument> GetOrCreateAsync(string userId, CancellationToken cancellationToken)
        {
            var profile = await _playerProfileRepository.GetAsync(userId, cancellationToken);

            if (profile != null)
                return profile;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var created = new PlayerProfileDocument
            {
                Id = userId,
                CreatedAt = now,
                UpdatedAt = now,
                Rev = 1,
            };

            if (await _playerProfileRepository.InsertIfMissingAsync(created, cancellationToken))
            {
                _logger.LogInformation("[Player] profile created userId = {UserId}", userId);

                return created;
            }

            var existing = await _playerProfileRepository.GetAsync(userId, cancellationToken);

            return existing ?? created;
        }

        public async Task<PlayerUpdateResult> UpdateLoadoutAsync(
            string userId,
            LoadoutUpdate update,
            string requestId,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            var profile = await GetOrCreateAsync(userId, cancellationToken);
            var completed = await TryGetCompletedAsync(userId, requestId, cancellationToken);

            if (completed)
                return new PlayerUpdateResult(profile, false, new List<string>());

            var errors = Validate(profile, update, configDistributor);

            if (0 < errors.Count)
                return new PlayerUpdateResult(null, false, errors);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expectedRev = profile.Rev;

            profile.Loadout = new PlayerLoadoutDocument
            {
                CharacterId = update.CharacterId,
                Equipment = new Dictionary<string, string>(),
                Summons = new List<int>(update.Summons),
            };

            foreach (var pair in update.Equipment)
                profile.Loadout.Equipment[pair.Key] = pair.Value;

            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
            {
                await ReleaseAsync(userId, requestId, cancellationToken);

                return new PlayerUpdateResult(null, true, new List<string>());
            }

            await CompleteAsync(userId, requestId, profile.Rev, now, cancellationToken);

            return new PlayerUpdateResult(profile, false, new List<string>());
        }

        private List<string> Validate(PlayerProfileDocument profile, LoadoutUpdate update, IConfigDistributor configDistributor)
        {
            var errors = new List<string>();

            if (OwnsCharacter(profile, update.CharacterId) == false)
                errors.Add($"Character {update.CharacterId} is not unlocked.");
            else if (configDistributor.Characters.TryGet(update.CharacterId, out _) == false)
                errors.Add($"Character {update.CharacterId} is missing in configs.");

            if (MaxSummons < update.Summons.Count)
                errors.Add($"Loadout allows at most {MaxSummons} summons.");

            var seenSummons = new HashSet<int>();

            for (int i = 0; i < update.Summons.Count; i++)
            {
                var summonId = update.Summons[i];

                if (seenSummons.Add(summonId) == false)
                    errors.Add($"Summon {summonId} is listed twice.");

                if (OwnsSummon(profile, summonId) == false)
                    errors.Add($"Summon {summonId} is not owned.");
                else if (configDistributor.Summons.TryGet(summonId, out _) == false)
                    errors.Add($"Summon {summonId} is missing in configs.");
            }

            var seenInstances = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in update.Equipment)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    continue;

                if (seenInstances.Add(pair.Value) == false)
                {
                    errors.Add($"Equipment {pair.Value} is used in two slots.");

                    continue;
                }

                var instance = FindEquipment(profile, pair.Value);

                if (instance == null)
                {
                    errors.Add($"Equipment {pair.Value} is not owned.");

                    continue;
                }

                if (configDistributor.Equipments.TryGet(instance.ConfigId, out var mapper) == false)
                {
                    errors.Add($"Equipment config {instance.ConfigId} is missing.");

                    continue;
                }

                if (_equipmentSlotReader.Matches(mapper.Type, pair.Key) == false)
                    errors.Add($"Equipment {pair.Value} has type {mapper.Type} and does not fit slot {pair.Key}.");
            }

            return errors;
        }

        private bool OwnsCharacter(PlayerProfileDocument profile, int characterId)
        {
            for (int i = 0; i < profile.Characters.Count; i++)
            {
                if (profile.Characters[i].ConfigId == characterId)
                    return true;
            }

            return false;
        }

        private bool OwnsSummon(PlayerProfileDocument profile, int summonId)
        {
            for (int i = 0; i < profile.Summons.Count; i++)
            {
                if (profile.Summons[i].ConfigId == summonId)
                    return true;
            }

            return false;
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

        private async Task<PlayerProfileDocument> EnsureStartContentAsync(
            PlayerProfileDocument profile,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (0 < profile.Characters.Count || 0 < profile.Summons.Count || 0 < profile.Equipment.Count)
                return profile;

            if (configDistributor.Constants.TryGet(StartContentConstant, out var constant) == false || string.IsNullOrWhiteSpace(constant.ConstantValue))
            {
                _logger.LogWarning("[Player] start content constant is missing key = {Key} userId = {UserId}", StartContentConstant, profile.Id);

                return profile;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expectedRev = profile.Rev;
            var entries = _rewardApplier.Apply(profile, constant.ConstantValue, configDistributor, now);

            if (entries.Count == 0)
            {
                _logger.LogWarning(
                    "[Player] start content granted nothing key = {Key} value = {Value} userId = {UserId}",
                    StartContentConstant,
                    constant.ConstantValue,
                    profile.Id);

                return profile;
            }

            if (profile.Loadout.CharacterId <= 0 && 0 < profile.Characters.Count)
                profile.Loadout.CharacterId = profile.Characters[0].ConfigId;

            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                return await GetOrCreateAsync(profile.Id, cancellationToken);

            _logger.LogInformation("[Player] start content granted userId = {UserId} rewards = {Rewards}", profile.Id, constant.ConstantValue);

            return profile;
        }

        private async Task<bool> TryGetCompletedAsync(string userId, string requestId, CancellationToken cancellationToken)
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
                Action = LoadoutAction,
                CreatedAt = now,
                UpdatedAt = now,
            };

            return await _idempotencyRepository.TryReserveAsync(reserved, cancellationToken) == false;
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
