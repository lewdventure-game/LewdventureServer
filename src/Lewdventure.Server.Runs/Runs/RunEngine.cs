using System.Globalization;
using Server.Battles;
using Server.Bonuses;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Perks;
using Server.Services;
using Server.Stories;

namespace Server.Runs
{
    internal sealed class RunEngine
    {
        private const string StartPerkGroupsConstant = "start_perk_groups";
        private const int MaxRewardPerkDepth = 4;

        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleParameterParser _battleParameterParser;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IBattleScriptDigest _battleScriptDigest;
        private readonly IBattleSimulationValidator _battleSimulationValidator;
        private readonly IBattleSimulatorService _battleSimulatorService;
        private readonly IUnitStateBuilder _unitStateBuilder;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly IConfigDistributor _configDistributor;
        private readonly ILogger<RunEngine> _logger;
        private readonly RewardApplier _rewardApplier;
        private readonly RunEventKeys _runEventKeys;
        private readonly RunRandomFactory _runRandomFactory;
        private readonly RunRecords _runRecords;
        private readonly RunSnapshotBuilder _runSnapshotBuilder;
        private readonly RunStageRoller _runStageRoller;

        public RunEngine(
            IBattleBonusService battleBonusService,
            IBattleParameterParser battleParameterParser,
            IBattleRewardParser battleRewardParser,
            IBattleScriptDigest battleScriptDigest,
            IBattleSimulationValidator battleSimulationValidator,
            IBattleSimulatorService battleSimulatorService,
            IUnitStateBuilder unitStateBuilder,
            IBonusWorkModeParser bonusWorkModeParser,
            IConfigDistributor configDistributor,
            ILogger<RunEngine> logger,
            RewardApplier rewardApplier,
            RunEventKeys runEventKeys,
            RunRandomFactory runRandomFactory,
            RunRecords runRecords,
            RunSnapshotBuilder runSnapshotBuilder,
            RunStageRoller runStageRoller)
        {
            _battleBonusService = battleBonusService;
            _battleParameterParser = battleParameterParser;
            _battleRewardParser = battleRewardParser;
            _battleScriptDigest = battleScriptDigest;
            _battleSimulationValidator = battleSimulationValidator;
            _battleSimulatorService = battleSimulatorService;
            _unitStateBuilder = unitStateBuilder;
            _bonusWorkModeParser = bonusWorkModeParser;
            _configDistributor = configDistributor;
            _logger = logger;
            _rewardApplier = rewardApplier;
            _runEventKeys = runEventKeys;
            _runRandomFactory = runRandomFactory;
            _runRecords = runRecords;
            _runSnapshotBuilder = runSnapshotBuilder;
            _runStageRoller = runStageRoller;
        }

        public IConfigDistributor ConfigDistributor => _configDistributor;

        public bool TryCreateRun(string runId, PlayerProfileDocument profile, IStoryLevelMapper level, long seed, string battleSeedKey, string configVersion, out RunDocument run, out List<string> errors)
        {
            var roll = _runStageRoller.Roll(level, _configDistributor, seed, 0);

            errors = roll.Errors;
            run = new RunDocument
            {
                Id = runId,
                StoryLevelId = level.Id,
                ConfigVersion = configVersion,
                Seed = seed,
                BattleSeedKey = battleSeedKey,
                RollIndex = roll.RollIndex,
                Stages = roll.Stages,
            };

            if (0 < roll.Errors.Count)
                return false;

            run.CurrentHealth = ReadStartHealth(profile, run);
            run.CurrentHealthMaxReference = run.CurrentHealth;
            run.PendingStartPerkGroups = ReadStartPerkGroups();

            RollPendingChoices(run, level);

            return true;
        }

        public bool IsFinished(RunDocument run)
        {
            return run.Stages.Count <= run.StageIndex && run.PendingChoice == null;
        }

        public async Task<RunEngineStep> AdvanceAsync(RunDocument run, IRunProfileAccess profiles, CancellationToken cancellationToken)
        {
            if (run.PendingBattle != null)
                return Failed("Run waits for battle finish.");

            if (run.PendingChoice != null)
                return Failed("Run waits for a choice.");

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return Failed($"Story level {run.StoryLevelId} is missing in configs.");

            if (run.Stages.Count <= run.StageIndex)
                return new RunEngineStep(RunEngineStepKind.LevelEnd, null, string.Empty, null);

            var stage = run.Stages[run.StageIndex];

            if (_configDistributor.StoryEvents.TryGet(stage.EventId, out var storyEvent) == false)
                return Failed($"Story event {stage.EventId} is missing in configs.");

            var parameters = new Dictionary<string, string>();

            _battleParameterParser.ParseKeyValues(storyEvent.EventParameters, parameters);

            if (storyEvent.EventType == StoryEventType.ForkEvent)
                return OpenFork(run, storyEvent, parameters);

            if (storyEvent.EventType == StoryEventType.Fight)
                return await ResolveFightAsync(profiles, run, stage, storyEvent, parameters, cancellationToken);

            return await ResolveDefaultAsync(profiles, run, level, stage, storyEvent, parameters, cancellationToken);
        }

        public async Task<RunEngineStep> ChooseAsync(RunDocument run, IReadOnlyList<int> picks, IRunProfileAccess profiles, CancellationToken cancellationToken)
        {
            if (run.PendingBattle != null)
                return Failed("Run waits for battle finish.");

            if (run.PendingChoice == null)
                return Failed("Run has no pending choice.");

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return Failed($"Story level {run.StoryLevelId} is missing in configs.");

            if (string.Equals(run.PendingChoice.Kind, RunPendingChoiceDocument.PerkKind, StringComparison.Ordinal))
                return await ChoosePerksAsync(profiles, run, level, picks, cancellationToken);

            return await ChooseForkAsync(profiles, run, level, picks, cancellationToken);
        }

        public async Task<RunEngineStep> FinishBattleAsync(RunDocument run, IRunProfileAccess profiles, CancellationToken cancellationToken)
        {
            var pending = run.PendingBattle;

            if (pending == null)
                return Failed("Run has no pending battle.");

            if (pending.StageIndex != run.StageIndex || run.Stages.Count <= run.StageIndex)
                return Failed("Pending battle does not match the current stage.");

            if (_configDistributor.StoryLevels.TryGet(run.StoryLevelId, out var level) == false)
                return Failed($"Story level {run.StoryLevelId} is missing in configs.");

            var stage = run.Stages[run.StageIndex];

            if (_configDistributor.StoryEvents.TryGet(stage.EventId, out var storyEvent) == false)
                return Failed($"Story event {stage.EventId} is missing in configs.");

            var parameters = new Dictionary<string, string>();

            _battleParameterParser.ParseKeyValues(storyEvent.EventParameters, parameters);

            var profile = await profiles.GetAsync(cancellationToken);
            var outcome = new RunStepOutcome
            {
                EventType = "fight",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKeyEnd = ReadString(parameters, RunEventKeys.LocKeyEnd),
                BattleDigest = pending.BattleDigest,
                BattleStepCount = pending.BattleStepCount,
            };
            var battleFinish = new RunBattleFinish
            {
                StageIndex = run.StageIndex,
                HealthBefore = run.CurrentHealth,
                StageId = stage.StageId,
                EventId = storyEvent.Id,
                Outcome = pending.Outcome,
                Steps = pending.BattleStepCount,
            };

            run.PendingBattle = null;
            run.Statuses.Clear();
            ApplyPerkUsages(run, pending.PerkUsages);

            if (pending.IsWin == false)
            {
                run.Status = RunDocument.FailedStatus;
                run.IsNewRecord = _runRecords.IsNewRecord(profile, run);
                outcome.RunFailed = true;

                return new RunEngineStep(RunEngineStepKind.BattleLost, outcome, string.Empty, battleFinish);
            }

            var experience = storyEvent.LevelExperience;

            run.CurrentHealth = NormalizeRunHealth(pending.FinalHealth);

            var accountBonuses = new List<BattleReward>();

            ConsumeBattleBonuses(run);
            ApplyBattleBonusGrants(run, pending.BonusGrants, accountBonuses);

            if (await GrantProfileRewardsAsync(profiles, run, accountBonuses, StageGrantKey(run) + ":battle", outcome, cancellationToken) == false)
                return new RunEngineStep(RunEngineStepKind.Conflict, null, string.Empty, battleFinish);

            if (0 < accountBonuses.Count)
                profile = await profiles.GetAsync(cancellationToken);

            run.CurrentHealthMaxReference = ReadMaxHealth(profile, run);

            var grant = await ApplyRewardsAsync(profiles, run, ReadString(parameters, RunEventKeys.Rewards), StageGrantKey(run), outcome, cancellationToken);

            if (grant.IsConflict)
                return new RunEngineStep(RunEngineStepKind.Conflict, null, string.Empty, battleFinish);

            experience += grant.Experience;
            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            return new RunEngineStep(RunEngineStepKind.Stage, outcome, string.Empty, battleFinish);
        }

        private async Task<RunEngineStep> ResolveDefaultAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            IStoryLevelMapper level,
            RunStageDocument stage,
            IStoryEventMapper storyEvent,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var outcome = new RunStepOutcome
            {
                EventType = "default_event",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKey = ReadString(parameters, RunEventKeys.LocKey),
            };
            var experience = storyEvent.LevelExperience;

            var grant = await ApplyRewardsAsync(profiles, run, ReadString(parameters, RunEventKeys.Rewards), StageGrantKey(run), outcome, cancellationToken);

            if (grant.IsConflict)
                return Conflict();

            experience += grant.Experience;

            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            return Stage(outcome);
        }

        private RunEngineStep OpenFork(RunDocument run, IStoryEventMapper storyEvent, Dictionary<string, string> parameters)
        {
            run.PendingChoice = new RunPendingChoiceDocument
            {
                Kind = RunPendingChoiceDocument.ForkKind,
                Options = new List<int> { 1, 2 },
                ChoiceCount = 1,
            };

            var outcome = new RunStepOutcome
            {
                EventType = "fork_event",
                EventId = storyEvent.Id,
                StageId = run.Stages[run.StageIndex].StageId,
                LocKeyStart = ReadString(parameters, RunEventKeys.LocKeyStart),
                LocKey = ReadString(parameters, _runEventKeys.ForkButtonKey(1)),
                LocKeyEnd = ReadString(parameters, _runEventKeys.ForkButtonKey(2)),
            };

            return Stage(outcome);
        }

        private async Task<RunEngineStep> ChooseForkAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            IStoryLevelMapper level,
            IReadOnlyList<int> picks,
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

            var success = random.GetRandomValue(RandomRollNames.RunEvent) < NormalizeChance(chance);
            var branch = button == 1 ? (success ? 1 : 2) : (success ? 3 : 4);
            var rewards = ReadString(parameters, _runEventKeys.ForkRewardsKey(branch));
            var outcome = new RunStepOutcome
            {
                EventType = "fork_event",
                EventId = storyEvent.Id,
                StageId = stage.StageId,
                LocKey = ReadString(parameters, _runEventKeys.ForkLocKey(branch)),
            };
            var experience = storyEvent.LevelExperience;

            var grant = await ApplyRewardsAsync(profiles, run, rewards, StageGrantKey(run), outcome, cancellationToken);

            if (grant.IsConflict)
                return Conflict();

            experience += grant.Experience;

            run.PendingChoice = null;

            ApplyExperience(run, level, experience, outcome);
            ResolveStage(run, stage);

            outcome.ExperienceGained = experience;

            return Stage(outcome);
        }

        private async Task<RunEngineStep> ChoosePerksAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            IStoryLevelMapper level,
            IReadOnlyList<int> picks,
            CancellationToken cancellationToken)
        {
            var choice = run.PendingChoice!;

            if (picks.Count != choice.ChoiceCount)
                return Failed($"Choice needs exactly {choice.ChoiceCount} perks.");

            for (int i = 0; i < picks.Count; i++)
            {
                if (choice.Options.Contains(picks[i]) == false)
                    return Failed($"Perk {picks[i]} is not offered.");

                for (int previous = 0; previous < i; previous++)
                {
                    if (picks[previous] == picks[i])
                        return Failed($"Perk {picks[i]} is picked twice.");
                }
            }

            var outcome = new RunStepOutcome { EventType = "perk_choice" };
            var experience = 0;
            var choiceKey = "choice:" + run.RollIndex.ToString(CultureInfo.InvariantCulture);

            run.PendingChoice = null;

            for (int i = 0; i < picks.Count; i++)
            {
                var perkId = picks[i];

                run.Perks.Add(perkId);

                var grant = await ApplyRewardsAsync(profiles, run, ReadPerkGrantRewards(perkId), choiceKey + ":perk:" + perkId.ToString(CultureInfo.InvariantCulture), outcome, cancellationToken);

                if (grant.IsConflict)
                    return Conflict();

                experience += grant.Experience;
            }

            ApplyExperience(run, level, experience, outcome);

            return new RunEngineStep(RunEngineStepKind.PerksChosen, outcome, string.Empty, null);
        }

        private async Task<RunEngineStep> ResolveFightAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            RunStageDocument stage,
            IStoryEventMapper storyEvent,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var profile = await profiles.GetAsync(cancellationToken);
            var enemyIds = ReadIntList(parameters, RunEventKeys.Enemies);

            RescaleHealthToMax(profile, run);

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
                Seed = _runRandomFactory.CreateBattleSeed(run.Seed, run.StageIndex, run.BattleSeedKey),
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
                BattleInput = replayData,
                BattleDigest = _battleScriptDigest.Compute(script),
                BattleStepCount = script.Steps.Count,
            };

            run.PendingBattle = CreatePendingBattle(run, storyEvent.Id, script, profile.Loadout.CharacterId, outcome.BattleDigest);

            return new RunEngineStep(RunEngineStepKind.BattlePending, outcome, string.Empty, null);
        }

        private string ReadPerkGrantRewards(int perkId)
        {
            if (_configDistributor.Perks.TryGet(perkId, out var perkMapper) == false)
                return string.Empty;

            if (perkMapper.PerkType != PerkType.Reward && perkMapper.PerkType != PerkType.ActionReward)
                return string.Empty;

            var parameters = new Dictionary<string, string>();

            _battleParameterParser.ParseKeyValues(perkMapper.PerkParameters, parameters);

            return ReadString(parameters, RunEventKeys.Rewards);
        }

        private RunPendingBattleDocument CreatePendingBattle(RunDocument run, int eventId, IBattleScriptResponse script, int characterId, string battleDigest)
        {
            var pending = new RunPendingBattleDocument
            {
                StageIndex = run.StageIndex,
                EventId = eventId,
                IsWin = script.OutcomeType == OutcomeType.TeamAWin,
                Outcome = script.OutcomeType.ToString(),
                FinalHealth = ReadFinalHealth(script, characterId, run.CurrentHealth),
                BattleDigest = battleDigest,
                BattleStepCount = script.Steps.Count,
            };

            for (int i = 0; i < script.BonusGrants.Count; i++)
            {
                var grant = script.BonusGrants[i];

                if (grant == null)
                    continue;

                pending.BonusGrants.Add(new RunBonusDocument { BonusId = grant.Id, Count = grant.Count, RemainingBattles = grant.RemainingBattles });
            }

            for (int i = 0; i < script.PerkUsages.Count; i++)
            {
                var usage = script.PerkUsages[i];

                if (usage == null)
                    continue;

                pending.PerkUsages.Add(new RunPendingPerkUsageDocument { PerkId = usage.PerkId, UsedCount = usage.UsedCount, RemainingUses = usage.RemainingUses });
            }

            return pending;
        }

        private async Task<RunRewardGrant> ApplyRewardsAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            string rewards,
            string grantKey,
            RunStepOutcome outcome,
            CancellationToken cancellationToken)
        {
            return await ApplyRewardsAsync(profiles, run, rewards, grantKey, outcome, 0, cancellationToken);
        }

        private async Task<RunRewardGrant> ApplyRewardsAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            string rewards,
            string grantKey,
            RunStepOutcome outcome,
            int perkDepth,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rewards))
                return new RunRewardGrant(0, false);

            var parsed = _battleRewardParser.Parse(rewards);
            var profileRewards = new List<BattleReward>(parsed.Count);
            var healthRewards = new List<BattleReward>();
            var grantedPerks = new List<int>();
            var experience = 0;

            for (int i = 0; i < parsed.Count; i++)
            {
                var reward = parsed[i];

                if (reward.Type == BattleRewardType.Bonus && IsHealthEffectBonus(reward.Id))
                {
                    healthRewards.Add(reward);

                    continue;
                }

                if (reward.Type == BattleRewardType.Bonus && IsAccountBonus(reward.Id))
                {
                    profileRewards.Add(reward);

                    continue;
                }

                if (reward.Type == BattleRewardType.Bonus)
                {
                    AddRunBonus(run, reward.Id, reward.Count);

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

                if (reward.Type == BattleRewardType.Perk)
                {
                    AddRewardPerk(run, reward, grantedPerks, outcome);

                    continue;
                }

                if (reward.Type == BattleRewardType.PerkGroup)
                {
                    AddRewardPerkGroup(run, reward, outcome);

                    continue;
                }

                profileRewards.Add(reward);
            }

            if (0 < healthRewards.Count)
            {
                var profile = await profiles.GetAsync(cancellationToken);

                ApplyHealthRewards(profile, run, healthRewards);
            }

            if (await GrantProfileRewardsAsync(profiles, run, profileRewards, grantKey, outcome, cancellationToken) == false)
                return new RunRewardGrant(experience, true);

            for (int i = 0; i < grantedPerks.Count; i++)
            {
                var perkId = grantedPerks[i];
                var perkRewards = ReadPerkGrantRewards(perkId);

                if (string.IsNullOrWhiteSpace(perkRewards))
                    continue;

                if (MaxRewardPerkDepth <= perkDepth)
                {
                    _logger.LogWarning("[Run] perk {PerkId} rewards skipped, nested perk rewards are too deep runId = {RunId}", perkId, run.Id);

                    continue;
                }

                var perkKey = grantKey + ":perk:" + perkId.ToString(CultureInfo.InvariantCulture) + ":" + i.ToString(CultureInfo.InvariantCulture);
                var perkGrant = await ApplyRewardsAsync(profiles, run, perkRewards, perkKey, outcome, perkDepth + 1, cancellationToken);

                if (perkGrant.IsConflict)
                    return new RunRewardGrant(experience, true);

                experience += perkGrant.Experience;
            }

            return new RunRewardGrant(experience, false);
        }

        private async Task<bool> GrantProfileRewardsAsync(
            IRunProfileAccess profiles,
            RunDocument run,
            List<BattleReward> profileRewards,
            string grantKey,
            RunStepOutcome outcome,
            CancellationToken cancellationToken)
        {
            if (profileRewards.Count == 0)
                return true;

            var source = "run:" + run.Id;
            var stepKey = source + ":" + grantKey;
            var result = await profiles.GrantAsync(profileRewards, source, stepKey, cancellationToken);

            if (result.Conflict)
                return false;

            if (result.Succeeded == false)
                _logger.LogWarning("[Run] rewards not applied userId = {UserId} runId = {RunId} errors = {Errors}", run.UserId, run.Id, string.Join("; ", result.Errors));
            else
                CollectAppliedRewards(run, profileRewards, result.Profile!, outcome);

            return true;
        }

        private bool IsAccountBonus(int bonusId)
        {
            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
                return false;

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) == false)
                return false;

            return _rewardApplier.IsAccountScoped(workMode);
        }

        private void AddRewardPerk(RunDocument run, BattleReward reward, List<int> grantedPerks, RunStepOutcome outcome)
        {
            if (_configDistributor.Perks.TryGet(reward.Id, out _) == false)
            {
                _logger.LogWarning("[Run] reward perk {PerkId} is missing in configs runId = {RunId}", reward.Id, run.Id);

                return;
            }

            var added = 0;

            for (int count = 0; count < reward.Count; count++)
            {
                if (IsOfferable(run, reward.Id) == false)
                {
                    _logger.LogInformation("[Run] reward perk {PerkId} is already active and not repeatable runId = {RunId}", reward.Id, run.Id);

                    break;
                }

                run.Perks.Add(reward.Id);
                grantedPerks.Add(reward.Id);
                added += 1;
            }

            if (added == 0)
                return;

            outcome.AppliedRewards.Add(new RunAppliedReward { Type = BattleRewardType.Perk.ToString(), Id = reward.Id, RewardKey = string.Empty, Count = added });
        }

        private void AddRewardPerkGroup(RunDocument run, BattleReward reward, RunStepOutcome outcome)
        {
            if (_configDistributor.PerkGroups.TryGet(reward.Id, out _) == false)
            {
                _logger.LogWarning("[Run] reward perk group {GroupId} is missing in configs runId = {RunId}", reward.Id, run.Id);

                return;
            }

            for (int count = 0; count < reward.Count; count++)
                run.PendingPerkGroups.Add(reward.Id);

            outcome.AppliedRewards.Add(new RunAppliedReward { Type = BattleRewardType.PerkGroup.ToString(), Id = reward.Id, RewardKey = string.Empty, Count = reward.Count });
        }

        private string StageGrantKey(RunDocument run)
        {
            return "stage:" + run.StageIndex.ToString(CultureInfo.InvariantCulture);
        }

        private void CollectAppliedRewards(RunDocument run, List<BattleReward> profileRewards, PlayerProfileDocument profile, RunStepOutcome outcome)
        {
            for (int i = 0; i < profileRewards.Count; i++)
            {
                var reward = profileRewards[i];
                var type = reward.Type.ToString();
                var rewardKey = reward.HasStringRewardKey ? reward.RewardKey : string.Empty;

                outcome.AppliedRewards.Add(new RunAppliedReward
                {
                    Type = type,
                    Id = reward.Id,
                    RewardKey = rewardKey,
                    Count = reward.Count,
                });

                AddRewardTotal(run, type, reward.Id, rewardKey, reward.Count);
            }

            outcome.ProfileRev = profile.Rev;
        }

        private void AddRewardTotal(RunDocument run, string type, int id, string rewardKey, int count)
        {
            for (int i = 0; i < run.RewardTotals.Count; i++)
            {
                var total = run.RewardTotals[i];

                if (total.Id != id
                    || string.Equals(total.Type, type, StringComparison.Ordinal) == false
                    || string.Equals(total.RewardKey, rewardKey, StringComparison.Ordinal) == false)
                    continue;

                total.Count += count;

                return;
            }

            run.RewardTotals.Add(new RunRewardTotalDocument { Type = type, Id = id, RewardKey = rewardKey, Count = count });
        }

        private int ResolveRemainingBattles(int bonusId)
        {
            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
                return 0;

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) == false)
                return 0;

            if (workMode.TryGetPart(BonusWorkModeKind.NextBattles, out var nextBattlesPart))
                return nextBattlesPart.Count;

            if (workMode.Contains(BonusWorkModeKind.EndOfBattle))
                return 1;

            return 0;
        }

        private bool IsHealthEffectBonus(int bonusId)
        {
            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
                return false;

            return bonusMapper.BonusType == BonusType.Healing
                || bonusMapper.BonusType == BonusType.HealingFromMax
                || bonusMapper.BonusType == BonusType.CurrentHealthLocal;
        }

        private void ApplyHealthRewards(PlayerProfileDocument profile, RunDocument run, List<BattleReward> healthRewards)
        {
            RescaleHealthToMax(profile, run);

            var unitState = BuildRunUnitState(profile, run);

            if (unitState == null)
                return;

            var commands = new List<BattleCommand>();

            for (int i = 0; i < healthRewards.Count; i++)
                _battleBonusService.Grant(unitState, healthRewards[i].Id, healthRewards[i].Count, "run:health:" + i.ToString(CultureInfo.InvariantCulture), commands, 0);

            var health = NormalizeRunHealth(unitState.CharacteristicState.Health);

            _logger.LogInformation("[Run] health reward runId = {RunId} health = {Before} -> {After} max = {Max}", run.Id, run.CurrentHealth, health, unitState.CharacteristicState.MaxHealth);

            run.CurrentHealth = health;
            run.CurrentHealthMaxReference = unitState.CharacteristicState.MaxHealth;
        }

        private void RescaleHealthToMax(PlayerProfileDocument profile, RunDocument run)
        {
            if (run.CurrentHealth <= 0f || run.CurrentHealthMaxReference <= 0f)
                return;

            var maxHealth = ReadMaxHealth(profile, run);

            if (maxHealth <= 0f || MathF.Abs(maxHealth - run.CurrentHealthMaxReference) < 0.5f)
                return;

            var health = NormalizeRunHealth(run.CurrentHealth * maxHealth / run.CurrentHealthMaxReference);

            _logger.LogInformation("[Run] health rescaled runId = {RunId} health = {Before} -> {After} max = {OldMax} -> {NewMax}", run.Id, run.CurrentHealth, health, run.CurrentHealthMaxReference, maxHealth);

            run.CurrentHealth = health;
            run.CurrentHealthMaxReference = maxHealth;
        }

        private float ReadMaxHealth(PlayerProfileDocument profile, RunDocument run)
        {
            var unitState = BuildRunUnitState(profile, run);

            if (unitState == null)
                return 0f;

            return unitState.CharacteristicState.MaxHealth;
        }

        private IUnitState? BuildRunUnitState(PlayerProfileDocument profile, RunDocument run)
        {
            var character = _runSnapshotBuilder.FindCharacter(profile, profile.Loadout.CharacterId);

            if (character == null)
                return null;

            var unit = _runSnapshotBuilder.BuildMainUnit(profile, run, character, _configDistributor);
            var stageId = run.StageIndex < run.Stages.Count ? run.Stages[run.StageIndex].StageId : 0;
            var unitState = _unitStateBuilder.Build(unit, BattleSide.Attacking, false, run.StoryLevelId, stageId);

            _unitStateBuilder.ApplyTeamSummons(unitState, _runSnapshotBuilder.CollectSummonIds(profile), unit);

            return unitState;
        }

        private float NormalizeRunHealth(float health)
        {
            var rounded = MathF.Round(health, MidpointRounding.AwayFromZero);

            if (rounded < 1f)
                return 1f;

            return rounded;
        }

        private void ApplyBattleBonusGrants(RunDocument run, List<RunBonusDocument> grants, List<BattleReward> accountBonuses)
        {
            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];

                if (grant.Count <= 0)
                    continue;

                if (IsAccountBonus(grant.BonusId))
                {
                    accountBonuses.Add(new BattleReward(BattleRewardType.Bonus, grant.BonusId, grant.Count));

                    _logger.LogInformation("[Run] battle bonus goes to account runId = {RunId} bonusId = {BonusId} count = {Count}", run.Id, grant.BonusId, grant.Count);

                    continue;
                }

                AddRunBonus(run, grant.BonusId, grant.Count, grant.RemainingBattles);

                _logger.LogInformation("[Run] battle bonus kept runId = {RunId} bonusId = {BonusId} count = {Count} remainingBattles = {RemainingBattles}", run.Id, grant.BonusId, grant.Count, grant.RemainingBattles);
            }
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
            if (0 < experience)
            {
                run.Experience += experience;

                var levelUps = 0;

                while (_configDistributor.ExperienceLevelPatterns.TryGetOrLast(level.ExperienceLevelId, run.ExperienceLevel, out var pattern))
                {
                    if (pattern.ExperienceForNextLevel <= 0 || run.Experience < pattern.ExperienceForNextLevel)
                        break;

                    run.Experience -= pattern.ExperienceForNextLevel;
                    run.ExperienceLevel += 1;
                    levelUps += 1;
                    outcome.LevelUps.Add(run.ExperienceLevel);
                }

                run.PendingLevelUps += levelUps;
            }

            RollPendingChoices(run, level);
        }

        private List<int> ReadStartPerkGroups()
        {
            if (_configDistributor.Constants.TryGet(StartPerkGroupsConstant, out var constant) == false)
                return new List<int>();

            return ReadIntList(constant.ConstantValue);
        }

        private void RollPendingChoices(RunDocument run, IStoryLevelMapper level)
        {
            while (run.PendingChoice == null && 0 < run.PendingStartPerkGroups.Count)
            {
                var groupId = run.PendingStartPerkGroups[0];

                run.PendingStartPerkGroups.RemoveAt(0);
                run.PendingChoice = RollGroupPerkChoice(run, groupId);

                if (run.PendingChoice == null)
                    _logger.LogWarning("[Run] start perk group {GroupId} gave no perk choice runId = {RunId} pending = {Pending}", groupId, run.Id, run.PendingStartPerkGroups.Count);
            }

            while (run.PendingChoice == null && 0 < run.PendingPerkGroups.Count)
            {
                var groupId = run.PendingPerkGroups[0];

                run.PendingPerkGroups.RemoveAt(0);
                run.PendingChoice = RollGroupPerkChoice(run, groupId);

                if (run.PendingChoice == null)
                    _logger.LogWarning("[Run] reward perk group {GroupId} gave no perk choice runId = {RunId} pending = {Pending}", groupId, run.Id, run.PendingPerkGroups.Count);
            }

            while (run.PendingChoice == null && 0 < run.PendingLevelUps)
            {
                var reachedLevel = run.ExperienceLevel - run.PendingLevelUps + 1;

                run.PendingLevelUps -= 1;
                run.PendingChoice = RollPerkChoice(run, level, reachedLevel);

                if (run.PendingChoice == null)
                    _logger.LogWarning("[Run] level up gave no perk choice runId = {RunId} level = {Level} pending = {Pending}", run.Id, reachedLevel, run.PendingLevelUps);
            }
        }

        private void CollectOfferablePerks(RunDocument run, IPerkGroupMapper group, out int[] offerableIds, out int[] offerableChances)
        {
            var ids = new List<int>(group.PerkIds.Length);
            var chances = new List<int>(group.PerkIds.Length);

            for (int i = 0; i < group.PerkIds.Length; i++)
            {
                var perkId = group.PerkIds[i];
                var chance = i < group.PerkChances.Length ? group.PerkChances[i] : 0;

                if (IsOfferable(run, perkId) == false)
                {
                    _logger.LogDebug("[Run] perk {PerkId} is already active and not repeatable runId = {RunId}", perkId, run.Id);

                    continue;
                }

                ids.Add(perkId);
                chances.Add(chance);
            }

            offerableIds = ids.ToArray();
            offerableChances = chances.ToArray();
        }

        private bool IsOfferable(RunDocument run, int perkId)
        {
            if (run.Perks.Contains(perkId) == false)
                return true;

            if (_configDistributor.Perks.TryGet(perkId, out var perkMapper) == false)
                return false;

            return perkMapper.IsMultiplePicks;
        }

        private void ApplyPerkUsages(RunDocument run, List<RunPendingPerkUsageDocument> usages)
        {
            for (int i = 0; i < usages.Count; i++)
            {
                var usage = usages[i];

                if (0 < usage.RemainingUses)
                {
                    StorePerkUsage(run, usage.PerkId, usage.UsedCount);

                    continue;
                }

                run.Perks.Remove(usage.PerkId);
                RemovePerkUsage(run, usage.PerkId);

                _logger.LogInformation("[Run] perk {PerkId} is exhausted and removed runId = {RunId} used = {Used}", usage.PerkId, run.Id, usage.UsedCount);
            }
        }

        private void StorePerkUsage(RunDocument run, int perkId, int usedCount)
        {
            for (int i = 0; i < run.PerkUsages.Count; i++)
            {
                if (run.PerkUsages[i].PerkId != perkId)
                    continue;

                run.PerkUsages[i].UsedCount = usedCount;

                return;
            }

            if (usedCount <= 0)
                return;

            run.PerkUsages.Add(new RunPerkUsageDocument { PerkId = perkId, UsedCount = usedCount });
        }

        private void RemovePerkUsage(RunDocument run, int perkId)
        {
            for (int i = run.PerkUsages.Count - 1; 0 <= i; i--)
            {
                if (run.PerkUsages[i].PerkId == perkId)
                    run.PerkUsages.RemoveAt(i);
            }
        }

        private RunPendingChoiceDocument? RollPerkChoice(RunDocument run, IStoryLevelMapper level, int reachedLevel)
        {
            if (_configDistributor.ExperienceLevelPatterns.TryGetOrLast(level.ExperienceLevelId, reachedLevel, out var pattern) == false)
                return null;

            if (pattern.PerkPresetIds.Length == 0)
                return null;

            var groupRandom = _runRandomFactory.Create(run.Seed, run.RollIndex);

            run.RollIndex += 1;

            var groupId = _runRandomFactory.PickWeighted(groupRandom, pattern.PerkPresetIds, pattern.PerkPresetWeights);

            return RollGroupPerkChoice(run, groupId);
        }

        private RunPendingChoiceDocument? RollGroupPerkChoice(RunDocument run, int groupId)
        {
            if (_configDistributor.PerkGroups.TryGet(groupId, out var group) == false)
            {
                _logger.LogWarning("[Run] perk group {GroupId} is missing in configs", groupId);

                return null;
            }

            var perkRandom = _runRandomFactory.Create(run.Seed, run.RollIndex);

            run.RollIndex += 1;

            CollectOfferablePerks(run, group, out var offerableIds, out var offerableChances);

            if (offerableIds.Length == 0)
            {
                _logger.LogInformation("[Run] perk group {GroupId} has no offerable perks left runId = {RunId}", groupId, run.Id);

                return null;
            }

            var count = group.RandomPerksCount <= 0 ? offerableIds.Length : group.RandomPerksCount;
            var options = _runRandomFactory.PickDistinct(perkRandom, offerableIds, offerableChances, count);

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

        private float ReadStartHealth(PlayerProfileDocument profile, RunDocument run)
        {
            var character = _runSnapshotBuilder.FindCharacter(profile, profile.Loadout.CharacterId);

            if (character == null)
                return 0f;

            var unit = _runSnapshotBuilder.BuildMainUnit(profile, run, character, _configDistributor);
            var stageId = run.Stages.Count == 0 ? 0 : run.Stages[0].StageId;
            var unitState = _unitStateBuilder.Build(unit, BattleSide.Attacking, false, run.StoryLevelId, stageId);

            _unitStateBuilder.ApplyTeamSummons(unitState, _runSnapshotBuilder.CollectSummonIds(profile), unit);

            return unitState.CharacteristicState.MaxHealth;
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
            return ReadIntList(ReadString(parameters, key));
        }

        private List<int> ReadIntList(string raw)
        {
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
        private void AddRunBonus(RunDocument run, int bonusId, int count)
        {
            AddRunBonus(run, bonusId, count, ResolveRemainingBattles(bonusId));
        }

        private void AddRunBonus(RunDocument run, int bonusId, int count, int remainingBattles)
        {
            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                var existing = run.Bonuses[i];

                if (existing.BonusId != bonusId || existing.RemainingBattles != remainingBattles)
                    continue;

                existing.Count += count;

                return;
            }

            run.Bonuses.Add(new RunBonusDocument { BonusId = bonusId, Count = count, RemainingBattles = remainingBattles });
        }

        private RunEngineStep Stage(RunStepOutcome outcome)
        {
            return new RunEngineStep(RunEngineStepKind.Stage, outcome, string.Empty, null);
        }

        private RunEngineStep Failed(string error)
        {
            return new RunEngineStep(RunEngineStepKind.Failed, null, error, null);
        }

        private RunEngineStep Conflict()
        {
            return new RunEngineStep(RunEngineStepKind.Conflict, null, string.Empty, null);
        }
    }
}
