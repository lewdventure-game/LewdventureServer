using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerDialogueService
    {
        private const int MaxAttempts = 3;

        private readonly DialogueLaunchKeys _dialogueLaunchKeys = new();
        private readonly DialoguePathValidator _dialoguePathValidator = new();

        private readonly DialogueAnalytics _dialogueAnalytics;
        private readonly ILogger<PlayerDialogueService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly TimeProvider _timeProvider;

        public PlayerDialogueService(
            DialogueAnalytics dialogueAnalytics,
            ILogger<PlayerDialogueService> logger,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            TimeProvider timeProvider)
        {
            _dialogueAnalytics = dialogueAnalytics;
            _logger = logger;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerUpdateResult> CompleteAsync(
            string userId,
            DialogueCompletion completion,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);

                if (IsCompleted(profile, completion.LaunchKey))
                    return new PlayerUpdateResult(profile, false, new List<string>());

                var pendingIndex = FindPending(profile, completion.LaunchKey);
                var error = ResolveDialogueId(profile, completion.LaunchKey, pendingIndex, configDistributor, out var dialogueId);

                if (string.IsNullOrEmpty(error))
                    error = _dialoguePathValidator.Validate(dialogueId, completion.Picks, configDistributor);

                if (string.IsNullOrEmpty(error) == false)
                    return new PlayerUpdateResult(null, false, new List<string> { error });

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var expectedRev = profile.Rev;

                if (0 <= pendingIndex)
                    profile.Dialogues.Pending.RemoveAt(pendingIndex);

                profile.Dialogues.Completed.Add(new PlayerDialogueCompletionDocument
                {
                    LaunchKey = completion.LaunchKey,
                    DialogueId = dialogueId,
                    Picks = new List<int>(completion.Picks),
                    CompletedAt = now,
                });
                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = now;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                    continue;

                _dialogueAnalytics.TrackCompleted(userId, completion.LaunchKey, dialogueId, completion.Picks);
                _logger.LogInformation("[Player] dialogue completed userId = {UserId} launchKey = {LaunchKey} picks = {Picks}", userId, completion.LaunchKey, completion.Picks.Count);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            return new PlayerUpdateResult(null, true, new List<string>());
        }

        private string ResolveDialogueId(
            PlayerProfileDocument profile,
            string launchKey,
            int pendingIndex,
            IConfigDistributor configDistributor,
            out int dialogueId)
        {
            dialogueId = 0;

            if (0 <= pendingIndex)
            {
                dialogueId = profile.Dialogues.Pending[pendingIndex].DialogueId;

                return string.Empty;
            }

            if (_dialogueLaunchKeys.IsReward(launchKey))
                return $"Dialogue launch {launchKey} is not pending.";

            if (_dialogueLaunchKeys.TryParseLevel(launchKey, out var storyLevelId, out dialogueId, out var trigger) == false)
                return $"Dialogue launch {launchKey} is not recognized.";

            if (configDistributor.StoryLevels.TryGet(storyLevelId, out var level) == false)
                return $"Story level {storyLevelId} is missing in configs.";

            var count = Math.Min(level.DialogueIds.Length, level.DialogueTriggers.Length);

            for (int i = 0; i < count; i++)
            {
                if (level.DialogueIds[i] == dialogueId && string.Equals(_dialogueLaunchKeys.NormalizeTrigger(level.DialogueTriggers[i].Source), trigger, StringComparison.Ordinal))
                    return string.Empty;
            }

            return $"Story level {storyLevelId} has no dialogue {dialogueId} on {trigger}.";
        }

        private bool IsCompleted(PlayerProfileDocument profile, string launchKey)
        {
            var completed = profile.Dialogues.Completed;

            for (int i = 0; i < completed.Count; i++)
            {
                if (string.Equals(completed[i].LaunchKey, launchKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private int FindPending(PlayerProfileDocument profile, string launchKey)
        {
            var pending = profile.Dialogues.Pending;

            for (int i = 0; i < pending.Count; i++)
            {
                if (string.Equals(pending[i].LaunchKey, launchKey, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
