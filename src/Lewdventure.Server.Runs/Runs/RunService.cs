using System.Globalization;
using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Services;
using Server.GameConfigs;
using Server.Stories;

namespace Server.Runs
{
    internal sealed class RunService
    {
        private const string RunIdPrefix = "run_";
        private const int MaxAttempts = 3;

        private readonly IBattleParameterParser _battleParameterParser;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IBattleSimulationValidator _battleSimulationValidator;
        private readonly BattleSimulatorService _battleSimulatorService;
        private readonly IConfigDistributor _configDistributor;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly ILogger<RunService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly PlayerRewardService _playerRewardService;
        private readonly RunEventKeys _runEventKeys;
        private readonly RunRandomFactory _runRandomFactory;
        private readonly RunRepository _runRepository;
        private readonly RunSnapshotBuilder _runSnapshotBuilder;
        private readonly RunStageRoller _runStageRoller;
        private readonly TimeProvider _timeProvider;

        public RunService(
            IBattleParameterParser battleParameterParser,
            IBattleRewardParser battleRewardParser,
            IBattleSimulationValidator battleSimulationValidator,
            BattleSimulatorService battleSimulatorService,
            IConfigDistributor configDistributor,
            IGameConfigSetProvider gameConfigSetProvider,
            ILogger<RunService> logger,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            PlayerRewardService playerRewardService,
            RunEventKeys runEventKeys,
            RunRandomFactory runRandomFactory,
            RunRepository runRepository,
            RunSnapshotBuilder runSnapshotBuilder,
            RunStageRoller runStageRoller,
            TimeProvider timeProvider)
        {
            _battleParameterParser = battleParameterParser;
            _battleRewardParser = battleRewardParser;
            _battleSimulationValidator = battleSimulationValidator;
            _battleSimulatorService = battleSimulatorService;
            _configDistributor = configDistributor;
            _gameConfigSetProvider = gameConfigSetProvider;
            _logger = logger;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _playerRewardService = playerRewardService;
            _runEventKeys = runEventKeys;
            _runRandomFactory = runRandomFactory;
            _runRepository = runRepository;
            _runSnapshotBuilder = runSnapshotBuilder;
            _runStageRoller = runStageRoller;
            _timeProvider = timeProvider;
        }

        public async Task<RunOperationResult> GetCurrentAsync(string userId, CancellationToken cancellationToken)
        {
            var run = await _runRepository.GetActiveAsync(userId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            return new RunOperationResult(run, null, false, new List<string>());
        }

        public async Task<RunOperationResult> StartAsync(string userId, int storyLevelId, CancellationToken cancellationToken)
        {
            var active = await _runRepository.GetActiveAsync(userId, cancellationToken);

            if (active != null)
                return Failed($"Run {active.Id} is still active.");

            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var level) == false)
                return Failed($"Story level {storyLevelId} is missing in configs.");

            var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);

            if (IsUnlocked(level, profile) == false)
                return Failed($"Story level {storyLevelId} is locked.");

            if (profile.Loadout.CharacterId <= 0)
                return Failed("Loadout has no character.");

            var seed = _runRandomFactory.CreateSeed();
            var roll = _runStageRoller.Roll(level, _configDistributor, seed, 0);

            if (0 < roll.Errors.Count)
                return new RunOperationResult(null, null, false, roll.Errors);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var run = new RunDocument
            {
                Id = RunIdPrefix + Guid.NewGuid().ToString("N"),
                UserId = userId,
                StoryLevelId = storyLevelId,
                ConfigVersion = _gameConfigSetProvider.Current.Version,
                Seed = seed,
                RollIndex = roll.RollIndex,
                Stages = roll.Stages,
                CreatedAt = now,
                UpdatedAt = now,
                Rev = 1,
            };

            await _runRepository.InsertAsync(run, cancellationToken);

            if (await SetCurrentRunAsync(profile, run.Id, now, cancellationToken) == false)
                return Conflict();

            _logger.LogInformation("[Run] started userId = {UserId} runId = {RunId} level = {Level} stages = {Stages} config = {Config}", userId, run.Id, storyLevelId, run.Stages.Count, run.ConfigVersion);

            return new RunOperationResult(run, null, false, new List<string>());
        }

        public async Task<RunOperationResult> AdvanceAsync(string userId, string runId, string requestId, CancellationToken cancellationToken)
        {
            var run = await LoadActiveAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            if (run.PendingChoice != null)
                return Failed("Run waits for a choice.");

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return Failed($"Story level {run.StoryLevelId} is missing in configs.");

            if (run.Stages.Count <= run.StageIndex)
                return await CompleteAsync(run, cancellationToken);

            var stage = run.Stages[run.StageIndex];

            if (_configDistributor.StoryEvents.TryGet(stage.EventId, out var storyEvent) == false)
                return Failed($"Story event {stage.EventId} is missing in configs.");

            var parameters = new Dictionary<string, string>();

            _battleParameterParser.ParseKeyValues(storyEvent.EventParameters, parameters);

            if (storyEvent.EventType == StoryEventType.ForkEvent)
                return await OpenForkAsync(run, storyEvent, parameters, cancellationToken);

            if (storyEvent.EventType == StoryEventType.Fight)
                return await ResolveFightAsync(userId, run, level, stage, storyEvent, parameters, requestId, cancellationToken);

            return await ResolveDefaultAsync(userId, run, level, stage, storyEvent, parameters, requestId, cancellationToken);
        }

        public async Task<RunOperationResult> ChooseAsync(string userId, string runId, IReadOnlyList<int> picks, string requestId, CancellationToken cancellationToken)
        {
            var run = await LoadActiveAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            if (run.PendingChoice == null)
                return Failed("Run has no pending choice.");

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return Failed($"Story level {run.StoryLevelId} is missing in configs.");

            if (string.Equals(run.PendingChoice.Kind, RunPendingChoiceDocument.PerkKind, StringComparison.Ordinal))
                return await ChoosePerksAsync(run, level, picks, cancellationToken);

            return await ChooseForkAsync(userId, run, level, picks, requestId, cancellationToken);
        }

        public async Task<RunOperationResult> AbandonAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            var run = await LoadActiveAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            run.Status = RunDocument.AbandonedStatus;

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            await ClearCurrentRunAsync(userId, run.Id, cancellationToken);
            _logger.LogInformation("[Run] abandoned userId = {UserId} runId = {RunId} stage = {Stage}", userId, run.Id, run.StageIndex);

            return new RunOperationResult(run, null, false, new List<string>());
        }

        private async Task<RunOperationResult> ResolveDefaultAsync(
            string userId,
            RunDocument run,
            IStoryLevelMapper level,
            RunStageDocument stage,
            IStoryEventMapper storyEvent,
            Dictionary<string, string> parameters,
            string requestId,
            CancellationToken cancellationToken)
        {
            var outcome = new RunStepOutcome
            {
                EventType = "default_event",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKey = ReadString(parameters, RunEventKeys.LocKey),
            };
            var experience = storyEvent.RewardExperienceValue;

            experience += await ApplyRewardsAsync(userId, run, ReadString(parameters, RunEventKeys.Rewards), 0, requestId, cancellationToken);

            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            return new RunOperationResult(run, outcome, false, new List<string>());
        }

        private async Task<RunOperationResult> OpenForkAsync(
            RunDocument run,
            IStoryEventMapper storyEvent,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            run.PendingChoice = new RunPendingChoiceDocument
            {
                Kind = RunPendingChoiceDocument.ForkKind,
                Options = new List<int> { 1, 2 },
                ChoiceCount = 1,
            };

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            var outcome = new RunStepOutcome
            {
                EventType = "fork_event",
                EventId = storyEvent.Id,
                StageId = run.Stages[run.StageIndex].StageId,
                LocKeyStart = ReadString(parameters, RunEventKeys.LocKeyStart),
                LocKey = ReadString(parameters, _runEventKeys.ForkButtonKey(1)),
                LocKeyEnd = ReadString(parameters, _runEventKeys.ForkButtonKey(2)),
            };

            return new RunOperationResult(run, outcome, false, new List<string>());
        }

        private async Task<RunOperationResult> ChooseForkAsync(
            string userId,
            RunDocument run,
            IStoryLevelMapper level,
            IReadOnlyList<int> picks,
            string requestId,
            CancellationToken cancellationToken)
        {
            if (picks.Count != 1 || picks[0] < 1 || 2 < picks[0])
                return Failed("Fork choice must be 1 or 2.");

            var stage = run.Stages[run.StageIndex];

            if (_configDistributor.StoryEvents.TryGet(stage.EventId, out var storyEvent) == false)
                return Failed($"Story event {stage.EventId} is missing in configs.");

            var parameters = new Dictionary<string, string>();

            _battleParameterParser.ParseKeyValues(storyEvent.EventParameters, parameters);

            var button = picks[0];
            var chanceKey = _runEventKeys.ForkChanceKey(button == 1 ? 1 : 3);
            var chance = ReadFloat(parameters, chanceKey);
            var random = _runRandomFactory.Create(run.Seed, run.RollIndex);

            run.RollIndex += 1;

            var success = random.GetRandomValue() < NormalizeChance(chance);
            var branch = button == 1 ? (success ? 1 : 2) : (success ? 3 : 4);
            var rewards = ReadString(parameters, _runEventKeys.ForkRewardsKey(branch));
            var rewardLength = (int)ReadFloat(parameters, _runEventKeys.ForkRewardLengthKey(branch));
            var outcome = new RunStepOutcome
            {
                EventType = "fork_event",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKey = ReadString(parameters, _runEventKeys.ForkLocKey(branch)),
            };
            var experience = storyEvent.RewardExperienceValue;

            experience += await ApplyRewardsAsync(userId, run, rewards, rewardLength, requestId, cancellationToken);

            run.PendingChoice = null;

            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            return new RunOperationResult(run, outcome, false, new List<string>());
        }

        private async Task<RunOperationResult> ChoosePerksAsync(RunDocument run, IStoryLevelMapper level, IReadOnlyList<int> picks, CancellationToken cancellationToken)
        {
            var choice = run.PendingChoice!;

            if (picks.Count != choice.ChoiceCount)
                return Failed($"Choice needs exactly {choice.ChoiceCount} perks.");

            for (int i = 0; i < picks.Count; i++)
            {
                if (choice.Options.Contains(picks[i]) == false)
                    return Failed($"Perk {picks[i]} is not offered.");

                run.Perks.Add(picks[i]);
            }

            run.PendingChoice = null;

            if (0 < run.PendingLevelUps)
            {
                run.PendingLevelUps -= 1;
                run.PendingChoice = RollPerkChoice(run, level);
            }

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            return new RunOperationResult(run, null, false, new List<string>());
        }

        private async Task<RunOperationResult> ResolveFightAsync(
            string userId,
            RunDocument run,
            IStoryLevelMapper level,
            RunStageDocument stage,
            IStoryEventMapper storyEvent,
            Dictionary<string, string> parameters,
            string requestId,
            CancellationToken cancellationToken)
        {
            var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
            var enemyIds = ReadIntList(parameters, RunEventKeys.Enemies);

            if (_runSnapshotBuilder.TryBuild(profile, run, stage, enemyIds, _configDistributor, out var simulationData, out var buildError) == false)
                return Failed(buildError);

            if (_battleSimulationValidator.TryValidate(simulationData, out var validationError) == false)
                return Failed(validationError);

            var replayData = new BattleReplayData
            {
                TeamA = simulationData.TeamA,
                TeamB = simulationData.TeamB,
                StoryLevelId = simulationData.StoryLevelId,
                StageId = simulationData.StageId,
                Seed = _runRandomFactory.CreateBattleSeed(run.Seed, run.StageIndex),
            };
            var script = _battleSimulatorService.Replay(replayData);
            var outcome = new RunStepOutcome
            {
                EventType = "fight",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKeyStart = ReadString(parameters, RunEventKeys.LocKeyStart),
                LocKeyEnd = ReadString(parameters, RunEventKeys.LocKeyEnd),
                BattleScript = script,
            };

            if (script.OutcomeType != OutcomeType.TeamAWin)
            {
                run.Status = RunDocument.FailedStatus;
                outcome.RunFailed = true;

                if (await SaveAsync(run, cancellationToken) == false)
                    return Conflict();

                await ClearCurrentRunAsync(userId, run.Id, cancellationToken);
                _logger.LogInformation("[Run] failed userId = {UserId} runId = {RunId} stage = {Stage} outcome = {Outcome}", userId, run.Id, run.StageIndex, script.OutcomeType);

                return new RunOperationResult(run, outcome, false, new List<string>());
            }

            var experience = storyEvent.RewardExperienceValue;

            experience += await ApplyRewardsAsync(userId, run, ReadString(parameters, RunEventKeys.Rewards), 0, requestId, cancellationToken);

            run.CurrentHealth = ReadFinalHealth(script, profile.Loadout.CharacterId, run.CurrentHealth);

            ConsumeBattleBonuses(run);
            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            if (run.Stages.Count <= run.StageIndex && run.PendingChoice == null)
            {
                var completed = await CompleteAsync(run, cancellationToken);

                if (completed.Succeeded == false)
                    return completed;

                outcome.RunCompleted = true;
            }

            return new RunOperationResult(run, outcome, false, new List<string>());
        }

        private async Task<RunOperationResult> CompleteAsync(RunDocument run, CancellationToken cancellationToken)
        {
            run.Status = RunDocument.CompletedStatus;

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(run.UserId, cancellationToken);
                var expectedRev = profile.Rev;

                if (profile.Story.CompletedLevelIds.Contains(run.StoryLevelId) == false)
                    profile.Story.CompletedLevelIds.Add(run.StoryLevelId);

                if (string.Equals(profile.Story.CurrentRunId, run.Id, StringComparison.Ordinal))
                    profile.Story.CurrentRunId = string.Empty;

                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken))
                {
                    _logger.LogInformation("[Run] completed userId = {UserId} runId = {RunId} level = {Level}", run.UserId, run.Id, run.StoryLevelId);

                    var outcome = new RunStepOutcome { RunCompleted = true, EventType = "completed" };

                    return new RunOperationResult(run, outcome, false, new List<string>());
                }
            }

            return Conflict();
        }

        private async Task<int> ApplyRewardsAsync(string userId, RunDocument run, string rewards, int rewardLength, string requestId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rewards))
                return 0;

            var parsed = _battleRewardParser.Parse(rewards);
            var profileRewards = new List<BattleReward>(parsed.Count);
            var experience = 0;

            for (int i = 0; i < parsed.Count; i++)
            {
                var reward = parsed[i];

                if (reward.Type == BattleRewardType.Bonus)
                {
                    AddRunBonus(run, reward.Id, reward.Count, rewardLength);

                    continue;
                }

                if (reward.Type == BattleRewardType.Status)
                {
                    for (int count = 0; count < reward.Count; count++)
                        run.Statuses.Add(reward.Id);

                    continue;
                }

                if (reward.Type == BattleRewardType.Resource && reward.HasStringRewardKey && IsExperienceKey(reward.RewardKey))
                {
                    experience += reward.Count;

                    continue;
                }

                profileRewards.Add(reward);
            }

            if (0 < profileRewards.Count)
            {
                var stepRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId + ":" + run.StageIndex.ToString(CultureInfo.InvariantCulture);
                var source = "run:" + run.Id;
                var result = await _playerRewardService.GrantAsync(userId, profileRewards, source, stepRequestId, _configDistributor, cancellationToken);

                if (result.Succeeded == false)
                    _logger.LogWarning("[Run] rewards not applied userId = {UserId} runId = {RunId} errors = {Errors}", userId, run.Id, string.Join("; ", result.Errors));
            }

            return experience;
        }

        private void AddRunBonus(RunDocument run, int bonusId, int count, int rewardLength)
        {
            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                var existing = run.Bonuses[i];

                if (existing.BonusId != bonusId || existing.RemainingBattles != rewardLength)
                    continue;

                existing.Count += count;

                return;
            }

            run.Bonuses.Add(new RunBonusDocument { BonusId = bonusId, Count = count, RemainingBattles = rewardLength });
        }

        private void ConsumeBattleBonuses(RunDocument run)
        {
            for (int i = run.Bonuses.Count - 1; 0 <= i; i--)
            {
                var bonus = run.Bonuses[i];

                if (bonus.RemainingBattles <= 0)
                    continue;

                bonus.RemainingBattles -= 1;

                if (bonus.RemainingBattles <= 0)
                    run.Bonuses.RemoveAt(i);
            }
        }

        private void ApplyExperience(RunDocument run, IStoryLevelMapper level, int experience, RunStepOutcome outcome)
        {
            if (experience <= 0)
                return;

            run.Experience += experience;

            while (_configDistributor.ExperienceLevelPatterns.TryGet(level.ExperienceLevelId, run.ExperienceLevel, out var pattern))
            {
                if (pattern.ExperienceForNextLevel <= 0 || run.Experience < pattern.ExperienceForNextLevel)
                    break;

                run.Experience -= pattern.ExperienceForNextLevel;
                run.ExperienceLevel += 1;
                outcome.LevelUps.Add(run.ExperienceLevel);
            }

            if (outcome.LevelUps.Count == 0)
                return;

            run.PendingLevelUps += outcome.LevelUps.Count;

            if (run.PendingChoice != null)
                return;

            run.PendingLevelUps -= 1;
            run.PendingChoice = RollPerkChoice(run, level);
        }

        private RunPendingChoiceDocument? RollPerkChoice(RunDocument run, IStoryLevelMapper level)
        {
            if (_configDistributor.ExperienceLevelPatterns.TryGet(level.ExperienceLevelId, run.ExperienceLevel, out var pattern) == false)
                return null;

            if (pattern.PerkPresetIds.Length == 0)
                return null;

            var groupRandom = _runRandomFactory.Create(run.Seed, run.RollIndex);

            run.RollIndex += 1;

            var groupId = _runRandomFactory.PickWeighted(groupRandom, pattern.PerkPresetIds, pattern.PerkPresetWeights);

            if (_configDistributor.PerkGroups.TryGet(groupId, out var group) == false)
            {
                _logger.LogWarning("[Run] perk group {GroupId} is missing in configs", groupId);

                return null;
            }

            var perkRandom = _runRandomFactory.Create(run.Seed, run.RollIndex);

            run.RollIndex += 1;

            var count = group.RandomPerksCount <= 0 ? group.PerkIds.Length : group.RandomPerksCount;
            var options = _runRandomFactory.PickDistinct(perkRandom, group.PerkIds, group.PerkChances, count);

            if (options.Count == 0)
                return null;

            var choiceCount = group.ChoiceCount <= 0 ? 1 : group.ChoiceCount;

            if (options.Count < choiceCount)
                choiceCount = options.Count;

            return new RunPendingChoiceDocument
            {
                Kind = RunPendingChoiceDocument.PerkKind,
                Options = options,
                ChoiceCount = choiceCount,
            };
        }

        private void ResolveStage(RunDocument run, RunStageDocument stage)
        {
            stage.Resolved = true;
            run.StageIndex += 1;
        }

        private bool IsUnlocked(IStoryLevelMapper level, PlayerProfileDocument profile)
        {
            var types = level.TriggerTypes;

            for (int i = 0; i < types.Length; i++)
            {
                var value = level.TriggerValues.Length <= i ? 0 : level.TriggerValues[i];

                if (types[i] == StoryLevelTriggerType.AlwaysAvailable)
                    continue;

                if (types[i] == StoryLevelTriggerType.StoryLevelCompleted && profile.Story.CompletedLevelIds.Contains(value) == false)
                    return false;

                if (types[i] == StoryLevelTriggerType.StoryLevelNotCompleted && profile.Story.CompletedLevelIds.Contains(value))
                    return false;
            }

            return true;
        }

        private float ReadFinalHealth(IBattleScriptResponse script, int characterId, float fallback)
        {
            var health = fallback;

            for (int stepIndex = 0; stepIndex < script.Steps.Count; stepIndex++)
            {
                var commands = script.Steps[stepIndex].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.SetHp)
                        continue;

                    var unitId = command.Parameters["unitId"];
                    var slotIndex = command.Parameters["slotIndex"];

                    if (unitId == null || slotIndex == null || (int)unitId != characterId || (int)slotIndex != 0)
                        continue;

                    var value = command.Parameters["hp"];

                    if (value != null)
                        health = (float)value;
                }
            }

            return health;
        }

        private async Task<RunDocument?> LoadActiveAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            var run = string.IsNullOrEmpty(runId)
                ? await _runRepository.GetActiveAsync(userId, cancellationToken)
                : await _runRepository.GetAsync(runId, cancellationToken);

            if (run == null)
                return null;

            if (string.Equals(run.UserId, userId, StringComparison.Ordinal) == false)
                return null;

            if (string.Equals(run.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                return null;

            return run;
        }

        private async Task<bool> SaveAsync(RunDocument run, CancellationToken cancellationToken)
        {
            var expectedRev = run.Rev;

            run.Rev = expectedRev + 1;
            run.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            return await _runRepository.ReplaceAsync(run, expectedRev, cancellationToken);
        }

        private async Task<bool> SetCurrentRunAsync(PlayerProfileDocument profile, string runId, DateTime now, CancellationToken cancellationToken)
        {
            var expectedRev = profile.Rev;

            profile.Story.CurrentRunId = runId;
            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            return await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken);
        }

        private async Task ClearCurrentRunAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);

                if (string.Equals(profile.Story.CurrentRunId, runId, StringComparison.Ordinal) == false)
                    return;

                var expectedRev = profile.Rev;

                profile.Story.CurrentRunId = string.Empty;
                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken))
                    return;
            }

            _logger.LogWarning("[Run] current run not cleared userId = {UserId} runId = {RunId}", userId, runId);
        }

        private bool IsExperienceKey(string key)
        {
            return string.Equals(key, RunEventKeys.ExperienceResource, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, RunEventKeys.LegacyExperienceResource, StringComparison.OrdinalIgnoreCase);
        }

        private float NormalizeChance(float chance)
        {
            if (chance <= 1f)
                return chance;

            return chance / 100f;
        }

        private string ReadString(Dictionary<string, string> parameters, string key)
        {
            return parameters.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private float ReadFloat(Dictionary<string, string> parameters, string key)
        {
            var raw = ReadString(parameters, key);

            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        private List<int> ReadIntList(Dictionary<string, string> parameters, string key)
        {
            var raw = ReadString(parameters, key);
            var result = new List<int>();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    result.Add(value);
            }

            return result;
        }

        private RunOperationResult Failed(string error)
        {
            return new RunOperationResult(null, null, false, new List<string> { error });
        }

        private RunOperationResult Conflict()
        {
            return new RunOperationResult(null, null, true, new List<string>());
        }
    }
}
