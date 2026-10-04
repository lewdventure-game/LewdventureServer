using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Experiments;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentService
    {
        public const string CreateAction = "create";
        public const string StartAction = "start";
        public const string FreezeAction = "freeze";
        public const string RemoveAction = "remove";
        public const string FinishAction = "finish";
        public const string RolloutAction = "rollout";
        public const string DeleteAction = "delete";

        private readonly ConfigPublishingService _configPublishingService;
        private readonly ConfigSnapshotDiff _configSnapshotDiff;
        private readonly ExperimentAllocationValidator _experimentAllocationValidator;
        private readonly ExperimentChangeRepository _experimentChangeRepository;
        private readonly ExperimentRegistry _experimentRegistry;
        private readonly ExperimentRegistryLoader _experimentRegistryLoader;
        private readonly ExperimentRepository _experimentRepository;
        private readonly GameConfigSetCache _gameConfigSetCache;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly ILogger<ExperimentService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly UserRepository _userRepository;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public ExperimentService(
            ConfigPublishingService configPublishingService,
            ConfigSnapshotDiff configSnapshotDiff,
            ExperimentAllocationValidator experimentAllocationValidator,
            ExperimentChangeRepository experimentChangeRepository,
            ExperimentRegistry experimentRegistry,
            ExperimentRegistryLoader experimentRegistryLoader,
            ExperimentRepository experimentRepository,
            GameConfigSetCache gameConfigSetCache,
            IGameConfigSetProvider gameConfigSetProvider,
            ILogger<ExperimentService> logger,
            TimeProvider timeProvider,
            UserRepository userRepository)
        {
            _configPublishingService = configPublishingService;
            _configSnapshotDiff = configSnapshotDiff;
            _experimentAllocationValidator = experimentAllocationValidator;
            _experimentChangeRepository = experimentChangeRepository;
            _experimentRegistry = experimentRegistry;
            _experimentRegistryLoader = experimentRegistryLoader;
            _experimentRepository = experimentRepository;
            _gameConfigSetCache = gameConfigSetCache;
            _gameConfigSetProvider = gameConfigSetProvider;
            _logger = logger;
            _timeProvider = timeProvider;
            _userRepository = userRepository;
        }

        public async Task<List<ExperimentSummary>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            var experiments = await _experimentRepository.ListAsync(limit, cancellationToken);
            var summaries = new List<ExperimentSummary>(experiments.Count);

            for (int i = 0; i < experiments.Count; i++)
                summaries.Add(await CreateSummaryAsync(experiments[i], cancellationToken));

            return summaries;
        }

        public async Task<ExperimentSummary?> GetAsync(string experimentId, CancellationToken cancellationToken)
        {
            var experiment = await _experimentRepository.GetAsync(experimentId, cancellationToken);

            return experiment == null ? null : await CreateSummaryAsync(experiment, cancellationToken);
        }

        public async Task<List<ExperimentChangeDocument>> ListChangesAsync(string experimentId, int limit, CancellationToken cancellationToken)
        {
            return await _experimentChangeRepository.ListAsync(experimentId, limit, cancellationToken);
        }

        public async Task<ExperimentOperationResult> CreateAsync(ExperimentDocument draft, string actor, CancellationToken cancellationToken)
        {
            var result = new ExperimentOperationResult();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            draft.Status = ExperimentDocument.DraftStatus;
            draft.CreatedAt = now;
            draft.UpdatedAt = now;
            draft.UpdatedBy = actor;
            draft.StartedAt = null;
            draft.FinishedAt = null;
            draft.Rev = 1;

            for (int i = 0; i < draft.Groups.Count; i++)
                draft.Groups[i].Status = ExperimentGroupDocument.RecruitingStatus;

            _experimentAllocationValidator.ValidateStructure(draft, result.Errors);

            if (0 < result.Errors.Count)
            {
                result.Status = ExperimentOperationStatus.Invalid;

                return result;
            }

            await _lock.WaitAsync(cancellationToken);

            try
            {
                if (await _experimentRepository.InsertIfMissingAsync(draft, cancellationToken) == false)
                {
                    result.Status = ExperimentOperationStatus.Conflict;
                    result.Errors.Add($"Experiment '{draft.Id}' already exists.");

                    return result;
                }

                await JournalAsync(draft.Id, CreateAction, string.Empty, actor, string.Empty, now, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }

            result.Status = ExperimentOperationStatus.Ok;
            result.Experiment = draft;

            return result;
        }

        public async Task<ExperimentOperationResult> StartAsync(string experimentId, string actor, string reason, CancellationToken cancellationToken)
        {
            return await MutateAsync(experimentId, StartAction, string.Empty, actor, reason, StartMutationAsync, cancellationToken);
        }

        public async Task<ExperimentOperationResult> FreezeGroupAsync(string experimentId, string groupId, string actor, string reason, CancellationToken cancellationToken)
        {
            return await MutateAsync(experimentId, FreezeAction, groupId, actor, reason, FreezeMutationAsync, cancellationToken);
        }

        public async Task<ExperimentOperationResult> RemoveGroupAsync(string experimentId, string groupId, string actor, string reason, CancellationToken cancellationToken)
        {
            return await MutateAsync(experimentId, RemoveAction, groupId, actor, reason, RemoveMutationAsync, cancellationToken);
        }

        public async Task<ExperimentOperationResult> FinishAsync(string experimentId, string actor, string reason, CancellationToken cancellationToken)
        {
            return await MutateAsync(experimentId, FinishAction, string.Empty, actor, reason, FinishMutationAsync, cancellationToken);
        }

        public async Task<ExperimentOperationResult> RolloutAsync(string experimentId, string groupId, string actor, string reason, CancellationToken cancellationToken)
        {
            return await MutateAsync(experimentId, RolloutAction, groupId, actor, reason, RolloutMutationAsync, cancellationToken);
        }

        public async Task<ExperimentOperationResult> DeleteDraftAsync(string experimentId, string actor, CancellationToken cancellationToken)
        {
            var result = new ExperimentOperationResult();

            await _lock.WaitAsync(cancellationToken);

            try
            {
                if (await _experimentRepository.DeleteDraftAsync(experimentId, cancellationToken) == false)
                {
                    result.Status = ExperimentOperationStatus.Conflict;
                    result.Errors.Add($"Experiment '{experimentId}' is not a draft or does not exist.");

                    return result;
                }

                await JournalAsync(experimentId, DeleteAction, string.Empty, actor, string.Empty, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }

            result.Status = ExperimentOperationStatus.Ok;

            return result;
        }

        private async Task<ExperimentOperationResult> MutateAsync(
            string experimentId,
            string action,
            string groupId,
            string actor,
            string reason,
            Func<ExperimentMutation, Task> mutation,
            CancellationToken cancellationToken)
        {
            var result = new ExperimentOperationResult();

            await _lock.WaitAsync(cancellationToken);

            try
            {
                var experiment = await _experimentRepository.GetAsync(experimentId, cancellationToken);

                if (experiment == null)
                {
                    result.Status = ExperimentOperationStatus.NotFound;
                    result.Errors.Add($"Experiment '{experimentId}' is not found.");

                    return result;
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var expectedRev = experiment.Rev;
                var context = new ExperimentMutation(experiment, groupId, actor, reason, now, result, cancellationToken);

                await mutation(context);

                if (result.Status != ExperimentOperationStatus.Unknown)
                    return result;

                experiment.UpdatedAt = now;
                experiment.UpdatedBy = actor;
                experiment.Rev = expectedRev + 1;

                if (await _experimentRepository.ReplaceAsync(experiment, expectedRev, cancellationToken) == false)
                {
                    result.Status = ExperimentOperationStatus.Conflict;
                    result.Errors.Add($"Experiment '{experimentId}' was changed concurrently, retry.");

                    return result;
                }

                await JournalAsync(experimentId, action, groupId, actor, reason, now, cancellationToken);
                await _experimentRegistryLoader.ReloadAsync(cancellationToken);

                _logger.LogInformation("[Experiment] {Action} experiment = {ExperimentId} group = {GroupId} by = {Actor} reason = {Reason}", action, experimentId, groupId, actor, reason);

                result.Status = ExperimentOperationStatus.Ok;
                result.Experiment = experiment;

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task StartMutationAsync(ExperimentMutation mutation)
        {
            var experiment = mutation.Experiment;

            if (RequireStatus(mutation, ExperimentDocument.DraftStatus) == false)
                return;

            experiment.Status = ExperimentDocument.RunningStatus;
            experiment.StartedAt = mutation.Now;

            _experimentAllocationValidator.ValidateStructure(experiment, mutation.Result.Errors);
            _experimentAllocationValidator.ValidateAllocation(experiment, _experimentRegistry.Running, mutation.Result.Errors);
            await ValidateSnapshotsAsync(experiment, mutation.Result.Errors, mutation.CancellationToken);

            if (0 < mutation.Result.Errors.Count)
                mutation.Result.Status = ExperimentOperationStatus.Invalid;
        }

        private Task FreezeMutationAsync(ExperimentMutation mutation)
        {
            if (RequireStatus(mutation, ExperimentDocument.RunningStatus) == false)
                return Task.CompletedTask;

            var group = FindGroup(mutation);

            if (group == null)
                return Task.CompletedTask;

            if (string.Equals(group.Status, ExperimentGroupDocument.RecruitingStatus, StringComparison.Ordinal) == false)
            {
                mutation.Result.Status = ExperimentOperationStatus.Conflict;
                mutation.Result.Errors.Add($"Group '{group.Id}' is {group.Status}, only recruiting groups can be frozen.");

                return Task.CompletedTask;
            }

            group.Status = ExperimentGroupDocument.FrozenStatus;
            group.FrozenAt = mutation.Now;

            return Task.CompletedTask;
        }

        private Task RemoveMutationAsync(ExperimentMutation mutation)
        {
            if (RequireStatus(mutation, ExperimentDocument.RunningStatus) == false)
                return Task.CompletedTask;

            var group = FindGroup(mutation);

            if (group == null)
                return Task.CompletedTask;

            if (string.Equals(group.Status, ExperimentGroupDocument.RemovedStatus, StringComparison.Ordinal))
            {
                mutation.Result.Status = ExperimentOperationStatus.Conflict;
                mutation.Result.Errors.Add($"Group '{group.Id}' is already removed.");

                return Task.CompletedTask;
            }

            group.Status = ExperimentGroupDocument.RemovedStatus;
            group.RemovedAt = mutation.Now;

            return Task.CompletedTask;
        }

        private Task FinishMutationAsync(ExperimentMutation mutation)
        {
            if (RequireStatus(mutation, ExperimentDocument.RunningStatus) == false)
                return Task.CompletedTask;

            mutation.Experiment.Status = ExperimentDocument.FinishedStatus;
            mutation.Experiment.FinishedAt = mutation.Now;

            return Task.CompletedTask;
        }

        private async Task RolloutMutationAsync(ExperimentMutation mutation)
        {
            if (RequireStatus(mutation, ExperimentDocument.RunningStatus) == false)
                return;

            var group = FindGroup(mutation);

            if (group == null)
                return;

            var reason = string.IsNullOrEmpty(mutation.Reason) ? $"rollout {mutation.Experiment.Id}/{group.Id}" : mutation.Reason;
            var activation = await _configPublishingService.ActivateAsync(group.SnapshotVersion, mutation.Actor, reason, mutation.CancellationToken);

            mutation.Result.Warnings.AddRange(activation.Warnings);

            if (activation.Succeeded == false)
            {
                mutation.Result.Status = ExperimentOperationStatus.Invalid;
                mutation.Result.Errors.AddRange(activation.Errors);

                return;
            }

            mutation.Experiment.Status = ExperimentDocument.FinishedStatus;
            mutation.Experiment.FinishedAt = mutation.Now;
        }

        private bool RequireStatus(ExperimentMutation mutation, string status)
        {
            if (string.Equals(mutation.Experiment.Status, status, StringComparison.Ordinal))
                return true;

            mutation.Result.Status = ExperimentOperationStatus.Conflict;
            mutation.Result.Errors.Add($"Experiment '{mutation.Experiment.Id}' is {mutation.Experiment.Status}, expected {status}.");

            return false;
        }

        private ExperimentGroupDocument? FindGroup(ExperimentMutation mutation)
        {
            var groups = mutation.Experiment.Groups;

            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i].Id, mutation.GroupId, StringComparison.Ordinal))
                    return groups[i];
            }

            mutation.Result.Status = ExperimentOperationStatus.NotFound;
            mutation.Result.Errors.Add($"Group '{mutation.GroupId}' is not found in experiment '{mutation.Experiment.Id}'.");

            return null;
        }

        private async Task ValidateSnapshotsAsync(ExperimentDocument experiment, List<string> errors, CancellationToken cancellationToken)
        {
            var master = _gameConfigSetProvider.Current;

            if (master.IsEmpty)
            {
                errors.Add("Master configs are not loaded.");

                return;
            }

            for (int i = 0; i < experiment.Groups.Count; i++)
            {
                var group = experiment.Groups[i];

                if (string.IsNullOrWhiteSpace(group.SnapshotVersion))
                    continue;

                var configSet = await _gameConfigSetCache.GetAsync(group.SnapshotVersion, cancellationToken);

                if (configSet == null)
                {
                    errors.Add($"Group '{group.Id}' snapshot {group.SnapshotVersion} is not published or does not build.");

                    continue;
                }

                ValidateCompatibility(group, master.Snapshot, configSet.Snapshot, errors);
            }
        }

        private void ValidateCompatibility(ExperimentGroupDocument group, GameConfigSnapshot master, GameConfigSnapshot snapshot, List<string> errors)
        {
            if (string.Equals(master.Version, snapshot.Version, StringComparison.Ordinal))
                return;

            var diffs = _configSnapshotDiff.Compare(master, snapshot);

            for (int i = 0; i < diffs.Count; i++)
            {
                if (0 < diffs[i].Removed.Count)
                    errors.Add($"Group '{group.Id}' snapshot removes {diffs[i].Domain} ids that exist in master: {string.Join(", ", diffs[i].Removed)}.");
            }
        }

        private async Task<ExperimentSummary> CreateSummaryAsync(ExperimentDocument experiment, CancellationToken cancellationToken)
        {
            var summary = new ExperimentSummary(experiment);

            for (int i = 0; i < experiment.Groups.Count; i++)
                summary.Participants[experiment.Groups[i].Id] = await _userRepository.CountInGroupAsync(experiment.Id, experiment.Groups[i].Id, cancellationToken);

            return summary;
        }

        private async Task JournalAsync(string experimentId, string action, string groupId, string actor, string reason, DateTime now, CancellationToken cancellationToken)
        {
            var change = new ExperimentChangeDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                UpdatedAt = now,
                ExperimentId = experimentId,
                Action = action,
                GroupId = groupId,
                Actor = actor,
                Reason = reason,
            };

            await _experimentChangeRepository.InsertAsync(change, cancellationToken);
        }
    }
}
