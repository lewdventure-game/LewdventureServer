using Server.Dialogues;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Runs
{
    internal sealed class RunDialogues
    {
        private readonly DialogueLaunchKeys _dialogueLaunchKeys = new();

        private readonly IConfigDistributor _configDistributor;
        private readonly PlayerProfileRepository _playerProfileRepository;

        public RunDialogues(IConfigDistributor configDistributor, PlayerProfileRepository playerProfileRepository)
        {
            _configDistributor = configDistributor;
            _playerProfileRepository = playerProfileRepository;
        }

        public async Task CollectAsync(RunOperationResult result, RunDialogueMoment moment, int stageNumber, CancellationToken cancellationToken)
        {
            var run = result.Run;

            if (result.Succeeded == false || run == null)
                return;

            var profile = await _playerProfileRepository.GetAsync(run.UserId, cancellationToken);

            if (profile == null)
                return;

            CollectLevel(run, moment, stageNumber, profile, result.Dialogues);
            CollectLevel(run, ResolveFinishMoment(run, moment), stageNumber, profile, result.Dialogues);
            CollectRewards(profile, result.Dialogues);
        }

        public async Task CollectCurrentAsync(RunOperationResult result, CancellationToken cancellationToken)
        {
            var run = result.Run;

            if (result.Succeeded == false || run == null)
                return;

            var profile = await _playerProfileRepository.GetAsync(run.UserId, cancellationToken);

            if (profile == null)
                return;

            if (run.StageIndex == 0)
                CollectLevel(run, RunDialogueMoment.Start, 0, profile, result.Dialogues);

            if (IsStageInProgress(run))
                CollectLevel(run, RunDialogueMoment.StageEntered, run.StageIndex + 1, profile, result.Dialogues);

            CollectLevel(run, ResolveFinishMoment(run, RunDialogueMoment.None), 0, profile, result.Dialogues);
            CollectRewards(profile, result.Dialogues);
        }

        private bool IsStageInProgress(RunDocument run)
        {
            if (run.PendingBattle != null)
                return true;

            return run.PendingChoice != null && string.Equals(run.PendingChoice.Kind, RunPendingChoiceDocument.ForkKind, StringComparison.Ordinal);
        }

        private RunDialogueMoment ResolveFinishMoment(RunDocument run, RunDialogueMoment moment)
        {
            if (moment == RunDialogueMoment.Win || moment == RunDialogueMoment.Loss)
                return RunDialogueMoment.None;

            if (string.Equals(run.Status, RunDocument.CompletedStatus, StringComparison.Ordinal))
                return RunDialogueMoment.Win;

            if (string.Equals(run.Status, RunDocument.FailedStatus, StringComparison.Ordinal)
                || string.Equals(run.Status, RunDocument.AbandonedStatus, StringComparison.Ordinal))
                return RunDialogueMoment.Loss;

            return RunDialogueMoment.None;
        }

        private void CollectLevel(RunDocument run, RunDialogueMoment moment, int stageNumber, PlayerProfileDocument profile, List<RunDialogueLaunch> launches)
        {
            if (moment == RunDialogueMoment.None)
                return;

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return;

            var count = Math.Min(level.DialogueIds.Length, level.DialogueTriggers.Length);

            for (int i = 0; i < count; i++)
            {
                var trigger = level.DialogueTriggers[i];

                if (Matches(trigger, moment, stageNumber) == false)
                    continue;

                var dialogueId = level.DialogueIds[i];
                var launchKey = _dialogueLaunchKeys.ForLevel(run.StoryLevelId, dialogueId, trigger.Source);

                if (IsCompleted(profile, launchKey))
                    continue;

                launches.Add(new RunDialogueLaunch(launchKey, dialogueId));
            }
        }

        private bool Matches(DialogueTrigger trigger, RunDialogueMoment moment, int stageNumber)
        {
            return moment switch
            {
                RunDialogueMoment.Start => trigger.Type == DialogueTriggerType.OnStart,
                RunDialogueMoment.StageEntered => trigger.Type == DialogueTriggerType.OnStageEntered && trigger.StageNumber == stageNumber,
                RunDialogueMoment.Win => trigger.Type == DialogueTriggerType.OnWin,
                RunDialogueMoment.Loss => trigger.Type == DialogueTriggerType.OnLoss,
                _ => false,
            };
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

        private void CollectRewards(PlayerProfileDocument profile, List<RunDialogueLaunch> launches)
        {
            var pending = profile.Dialogues.Pending;

            for (int i = 0; i < pending.Count; i++)
                launches.Add(new RunDialogueLaunch(pending[i].LaunchKey, pending[i].DialogueId));
        }
    }
}
