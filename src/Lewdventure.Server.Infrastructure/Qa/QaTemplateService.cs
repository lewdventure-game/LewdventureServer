using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Players;

namespace Server.Infrastructure.Qa
{
    internal sealed class QaTemplateService
    {
        private const int MaxAttempts = 3;
        private const int ListLimit = 200;
        private const int MaxNameLength = 80;
        private const int MaxDescriptionLength = 500;
        private const string CheatAction = "cheat";
        private const string TemplateEntry = "template";
        private const string CopyEntry = "copy";
        private const string ImportEntry = "import";

        private readonly ILogger<QaTemplateService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly QaAccountService _qaAccountService;
        private readonly QaTemplateRepository _qaTemplateRepository;
        private readonly TimeProvider _timeProvider;
        private readonly UserRepository _userRepository;

        public QaTemplateService(
            ILogger<QaTemplateService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            QaAccountService qaAccountService,
            QaTemplateRepository qaTemplateRepository,
            TimeProvider timeProvider,
            UserRepository userRepository)
        {
            _logger = logger;
            _playerLedgerRepository = playerLedgerRepository;
            _playerProfileRepository = playerProfileRepository;
            _qaAccountService = qaAccountService;
            _qaTemplateRepository = qaTemplateRepository;
            _timeProvider = timeProvider;
            _userRepository = userRepository;
        }

        public async Task<List<QaTemplateDocument>> ListAsync(CancellationToken cancellationToken)
        {
            return await _qaTemplateRepository.ListAsync(ListLimit, cancellationToken);
        }

        public async Task<QaTemplateDocument?> GetAsync(string templateId, CancellationToken cancellationToken)
        {
            return await _qaTemplateRepository.GetAsync(templateId, cancellationToken);
        }

        public async Task<string> SaveAsync(
            string userId,
            string? templateId,
            string? name,
            string? description,
            string configVersion,
            string actor,
            CancellationToken cancellationToken)
        {
            var key = _qaAccountService.NormalizeAlias(templateId);

            if (_qaAccountService.IsValidAlias(key) == false)
                return "Template key must be 2-32 characters: a-z, 0-9, - and _.";

            var title = name == null ? string.Empty : name.Trim();
            var details = description == null ? string.Empty : description.Trim();

            if (title.Length == 0 || MaxNameLength < title.Length)
                return $"Template name is required and must be at most {MaxNameLength} characters.";

            if (MaxDescriptionLength < details.Length)
                return $"Template description must be at most {MaxDescriptionLength} characters.";

            var profile = await _playerProfileRepository.GetAsync(userId, cancellationToken);

            if (profile == null)
                return $"Player {userId} has no profile yet: open the game with this account first.";

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var existing = await _qaTemplateRepository.GetAsync(key, cancellationToken);
            var snapshot = Copy(profile);

            snapshot.Id = string.Empty;
            snapshot.Story.CurrentRunId = string.Empty;

            await _qaTemplateRepository.UpsertAsync(new QaTemplateDocument
            {
                Id = key,
                Name = title,
                Description = details,
                CreatedBy = actor,
                CreatedAt = existing == null ? now : existing.CreatedAt,
                UpdatedAt = now,
                SourceUserId = userId,
                ConfigVersion = configVersion,
                Profile = snapshot,
            }, cancellationToken);

            _logger.LogInformation("[Qa] template saved template = {TemplateId} userId = {UserId} actor = {Actor} overwritten = {Overwritten}", key, userId, actor, existing != null);

            return string.Empty;
        }

        public async Task<bool> DeleteAsync(string templateId, string actor, CancellationToken cancellationToken)
        {
            var deleted = 0 < await _qaTemplateRepository.DeleteAsync(templateId, cancellationToken);

            if (deleted)
                _logger.LogInformation("[Qa] template deleted template = {TemplateId} actor = {Actor}", templateId, actor);

            return deleted;
        }

        public async Task<PlayerUpdateResult> ApplyTemplateAsync(string templateId, string targetUserId, string actor, CancellationToken cancellationToken)
        {
            var template = await _qaTemplateRepository.GetAsync(templateId, cancellationToken);

            if (template == null)
                return Failed($"Template {templateId} is not found.");

            return await ReplaceProfileAsync(targetUserId, template.Profile, TemplateEntry, templateId, actor, cancellationToken);
        }

        public async Task<PlayerUpdateResult> CopyProfileAsync(string sourceUserId, string targetUserId, string actor, CancellationToken cancellationToken)
        {
            if (string.Equals(sourceUserId, targetUserId, StringComparison.Ordinal))
                return Failed("Source and target players are the same.");

            var source = await _playerProfileRepository.GetAsync(sourceUserId, cancellationToken);

            if (source == null)
                return Failed($"Player {sourceUserId} has no profile.");

            return await ReplaceProfileAsync(targetUserId, source, CopyEntry, sourceUserId, actor, cancellationToken);
        }

        public async Task<PlayerUpdateResult> ImportProfileAsync(PlayerProfileDocument profile, string targetUserId, string actor, CancellationToken cancellationToken)
        {
            return await ReplaceProfileAsync(targetUserId, profile, ImportEntry, "bug-report", actor, cancellationToken);
        }

        private async Task<PlayerUpdateResult> ReplaceProfileAsync(
            string targetUserId,
            PlayerProfileDocument source,
            string entryType,
            string sourceKey,
            string actor,
            CancellationToken cancellationToken)
        {
            if (await _userRepository.GetAsync(targetUserId, cancellationToken) == null)
                return Failed($"User {targetUserId} is not found.");

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var existing = await _playerProfileRepository.GetAsync(targetUserId, cancellationToken);
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var profile = Copy(source);

                profile.Id = targetUserId;
                profile.SchemaVersion = PlayerProfileDocument.CurrentSchemaVersion;
                profile.CreatedAt = existing == null ? now : existing.CreatedAt;
                profile.UpdatedAt = now;
                profile.Story.CurrentRunId = existing == null ? string.Empty : existing.Story.CurrentRunId;

                bool saved;

                if (existing == null)
                {
                    profile.Rev = 1;
                    saved = await _playerProfileRepository.InsertIfMissingAsync(profile, cancellationToken);
                }
                else
                {
                    profile.Rev = existing.Rev + 1;
                    saved = await _playerProfileRepository.ReplaceAsync(profile, existing.Rev, cancellationToken);
                }

                if (saved == false)
                    continue;

                await _playerLedgerRepository.AppendAsync(new PlayerLedgerDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = targetUserId,
                    Action = CheatAction,
                    Source = actor + ":" + CheatAction + ":" + entryType + ":" + sourceKey,
                    RequestId = string.Empty,
                    Rev = profile.Rev,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Entries = new List<PlayerLedgerEntryDocument> { new() { Type = entryType, Key = sourceKey, Amount = 1 } },
                }, cancellationToken);

                _logger.LogInformation("[Cheat] profile replaced userId = {UserId} actor = {Actor} from = {EntryType}:{SourceKey} rev = {Rev}", targetUserId, actor, entryType, sourceKey, profile.Rev);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            _logger.LogWarning("[Cheat] profile replace conflict userId = {UserId} actor = {Actor}", targetUserId, actor);

            return new PlayerUpdateResult(null, true, new List<string>());
        }

        private PlayerProfileDocument Copy(PlayerProfileDocument profile)
        {
            return BsonSerializer.Deserialize<PlayerProfileDocument>(profile.ToBsonDocument());
        }

        private PlayerUpdateResult Failed(string error)
        {
            return new PlayerUpdateResult(null, false, new List<string> { error });
        }
    }
}
