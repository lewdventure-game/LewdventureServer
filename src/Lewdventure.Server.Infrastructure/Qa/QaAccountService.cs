using MongoDB.Driver;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Qa
{
    internal sealed class QaAccountService
    {
        private const string UserIdPrefix = "usr_";
        private const int MinAliasLength = 2;
        private const int MaxAliasLength = 32;
        private const int MaxQueryLength = 128;
        private const int ListLimit = 500;

        private readonly ExperimentRegistry _experimentRegistry;
        private readonly ILogger<QaAccountService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly TokenGenerator _tokenGenerator;
        private readonly UserRepository _userRepository;

        public QaAccountService(
            ExperimentRegistry experimentRegistry,
            ILogger<QaAccountService> logger,
            TimeProvider timeProvider,
            TokenGenerator tokenGenerator,
            UserRepository userRepository)
        {
            _experimentRegistry = experimentRegistry;
            _logger = logger;
            _timeProvider = timeProvider;
            _tokenGenerator = tokenGenerator;
            _userRepository = userRepository;
        }

        public string NormalizeAlias(string? alias)
        {
            return alias == null ? string.Empty : alias.Trim().ToLowerInvariant();
        }

        public bool IsValidAlias(string alias)
        {
            if (alias.Length < MinAliasLength || MaxAliasLength < alias.Length)
                return false;

            for (int i = 0; i < alias.Length; i++)
            {
                var symbol = alias[i];

                if (char.IsAsciiLetterLower(symbol) == false && char.IsAsciiDigit(symbol) == false && symbol != '-' && symbol != '_')
                    return false;
            }

            return true;
        }

        public async Task<QaMarkResult> MarkAsync(string userId, string? alias, string actor, CancellationToken cancellationToken)
        {
            var normalized = NormalizeAlias(alias);

            if (IsValidAlias(normalized) == false)
                return new QaMarkResult(false, $"Alias must be {MinAliasLength}-{MaxAliasLength} characters: a-z, 0-9, - and _.");

            var user = await _userRepository.GetAsync(userId, cancellationToken);

            if (user == null)
                return new QaMarkResult(true, string.Empty);

            var owner = await _userRepository.FindByQaAliasAsync(normalized, cancellationToken);

            if (owner != null && string.Equals(owner.Id, userId, StringComparison.Ordinal) == false)
                return new QaMarkResult(false, $"Alias {normalized} is already used by {owner.Id}.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var qa = new UserQaDocument { Alias = normalized, MarkedBy = actor, MarkedAt = now };

            try
            {
                if (await _userRepository.MarkQaAsync(userId, qa, now, cancellationToken) == false)
                    return new QaMarkResult(true, string.Empty);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return new QaMarkResult(false, $"Alias {normalized} is already used.");
            }

            _logger.LogInformation("[Qa] account marked userId = {UserId} alias = {Alias} actor = {Actor} experimentCleared = {ExperimentCleared}", userId, normalized, actor, user.Experiment != null);

            return new QaMarkResult(false, string.Empty);
        }

        public async Task<bool> UnmarkAsync(string userId, string actor, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var updated = await _userRepository.UnmarkQaAsync(userId, now, cancellationToken);

            if (updated)
                _logger.LogInformation("[Qa] account unmarked userId = {UserId} actor = {Actor}", userId, actor);

            return updated;
        }

        public async Task<QaMarkResult> ForceExperimentAsync(string userId, string? experimentId, string? groupId, string actor, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetAsync(userId, cancellationToken);

            if (user == null)
                return new QaMarkResult(true, string.Empty);

            if (user.Qa == null)
                return new QaMarkResult(false, "Only a QA account can be put into a group: mark it as QA first.");

            var experiment = experimentId == null ? string.Empty : experimentId.Trim();
            var group = groupId == null ? string.Empty : groupId.Trim();
            UserExperimentDocument? assignment = null;
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (experiment.Length != 0)
            {
                if (_experimentRegistry.TryGetActiveGroup(experiment, group, out _) == false)
                    return new QaMarkResult(false, $"Group {group} of experiment {experiment} is not running.");

                assignment = new UserExperimentDocument
                {
                    ExperimentId = experiment,
                    GroupId = group,
                    AssignedAt = now,
                    Country = user.LastCountry,
                    Forced = true,
                    ForcedBy = actor,
                };
            }

            if (await _userRepository.SetQaExperimentAsync(userId, assignment, now, cancellationToken) == false)
                return new QaMarkResult(false, "Account is not QA anymore.");

            _logger.LogInformation(
                "[Qa] experiment forced userId = {UserId} experiment = {ExperimentId} group = {GroupId} actor = {Actor}",
                userId,
                assignment == null ? "master" : assignment.ExperimentId,
                assignment == null ? string.Empty : assignment.GroupId,
                actor);

            return new QaMarkResult(false, string.Empty);
        }

        public async Task<List<UserDocument>> ListAsync(CancellationToken cancellationToken)
        {
            return await _userRepository.ListQaAsync(ListLimit, cancellationToken);
        }

        public async Task<List<QaPlayerMatch>> FindAsync(string? query, CancellationToken cancellationToken)
        {
            var matches = new List<QaPlayerMatch>();
            var value = query == null ? string.Empty : query.Trim();

            if (value.Length == 0 || MaxQueryLength < value.Length)
                return matches;

            if (value.StartsWith(UserIdPrefix, StringComparison.Ordinal))
                Add(matches, await _userRepository.GetAsync(value, cancellationToken), QaPlayerMatch.UserIdMatch);

            var alias = NormalizeAlias(value);

            if (IsValidAlias(alias))
                Add(matches, await _userRepository.FindByQaAliasAsync(alias, cancellationToken), QaPlayerMatch.AliasMatch);

            Add(matches, await _userRepository.FindByDeviceAsync(_tokenGenerator.Hash(value), cancellationToken), QaPlayerMatch.DeviceMatch);

            return matches;
        }

        private void Add(List<QaPlayerMatch> matches, UserDocument? user, string matchedBy)
        {
            if (user == null)
                return;

            for (int i = 0; i < matches.Count; i++)
            {
                if (string.Equals(matches[i].UserId, user.Id, StringComparison.Ordinal))
                    return;
            }

            matches.Add(new QaPlayerMatch(user.Id, user.Qa == null ? string.Empty : user.Qa.Alias, matchedBy));
        }
    }
}
