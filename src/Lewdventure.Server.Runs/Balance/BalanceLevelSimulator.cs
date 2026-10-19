using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Battles;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Logging;
using Server.Runs;
using Server.Services;

namespace Server.Balance
{
    internal sealed class BalanceLevelSimulator
    {
        private const string RunIdPrefix = "balance_";
        private const string UserId = "balance";
        private const int MaxStepsPerRun = 1000;
        private const int MaxFailedSeeds = 20;
        private const ulong PolicyMix = 0xD1B54A32D192ED03UL;

        private readonly BalanceSnapshotFactory _balanceSnapshotFactory;
        private readonly BalanceStatistics _balanceStatistics;
        private readonly RewardApplier _rewardApplier;
        private readonly RunEventKeys _runEventKeys;
        private readonly RunRandomFactory _runRandomFactory;
        private readonly RunRecords _runRecords;
        private readonly RunSnapshotBuilder _runSnapshotBuilder;
        private readonly RunStageRoller _runStageRoller;

        public BalanceLevelSimulator(
            BalanceSnapshotFactory balanceSnapshotFactory,
            BalanceStatistics balanceStatistics,
            RewardApplier rewardApplier,
            RunEventKeys runEventKeys,
            RunRandomFactory runRandomFactory,
            RunRecords runRecords,
            RunSnapshotBuilder runSnapshotBuilder,
            RunStageRoller runStageRoller)
        {
            _balanceSnapshotFactory = balanceSnapshotFactory;
            _balanceStatistics = balanceStatistics;
            _rewardApplier = rewardApplier;
            _runEventKeys = runEventKeys;
            _runRandomFactory = runRandomFactory;
            _runRecords = runRecords;
            _runSnapshotBuilder = runSnapshotBuilder;
            _runStageRoller = runStageRoller;
        }

        public async Task<BalanceLevelRun> RunAsync(
            BalanceSetup setup,
            int storyLevelId,
            long firstSeed,
            int runs,
            BalanceLevelPolicy policy,
            string configVersion,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (configDistributor.StoryLevels.TryGet(storyLevelId, out var level) == false)
                return new BalanceLevelRun(null, $"Story level {storyLevelId} is missing in configs.");

            if (configDistributor.Characters.TryGet(setup.CharacterId, out _) == false)
                return new BalanceLevelRun(null, $"Character {setup.CharacterId} is missing in configs.");

            var engine = CreateEngine(configDistributor);
            var tally = new BalanceLevelTally();
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < runs; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var seed = firstSeed + i;
                var profile = _balanceSnapshotFactory.CreateProfile(setup);
                var access = new BalanceProfileAccess(profile, configDistributor, _rewardApplier, now);

                if (engine.TryCreateRun(RunIdPrefix + seed.ToString(CultureInfo.InvariantCulture), profile, level, seed, string.Empty, configVersion, out var run, out var errors) == false)
                    return new BalanceLevelRun(null, string.Join("; ", errors));

                run.UserId = UserId;

                var error = await PlayAsync(engine, run, access, policy, seed, tally, cancellationToken);

                if (error.Length != 0)
                    return new BalanceLevelRun(null, $"Seed {seed}: {error}");
            }

            return new BalanceLevelRun(CreateResult(storyLevelId, firstSeed, runs, tally), string.Empty);
        }

        private async Task<string> PlayAsync(RunEngine engine, RunDocument run, BalanceProfileAccess access, BalanceLevelPolicy policy, long seed, BalanceLevelTally tally, CancellationToken cancellationToken)
        {
            var random = new SeededRandomService(unchecked((ulong)seed ^ PolicyMix));

            for (int step = 0; step < MaxStepsPerRun; step++)
            {
                var stageIndex = run.StageIndex;
                RunEngineStep result;

                if (run.PendingBattle != null)
                    result = await engine.FinishBattleAsync(run, access, cancellationToken);
                else if (run.PendingChoice != null)
                    result = await engine.ChooseAsync(run, Pick(run.PendingChoice, policy, random, tally), access, cancellationToken);
                else
                    result = await engine.AdvanceAsync(run, access, cancellationToken);

                if (result.Kind == RunEngineStepKind.Failed)
                    return result.Error;

                if (result.Kind == RunEngineStepKind.Conflict)
                    return "Run step conflicted.";

                if (result.BattleFinish != null)
                    tally.Battles += 1;

                if (result.Kind == RunEngineStepKind.BattleLost)
                {
                    tally.Stage(stageIndex).Deaths += 1;
                    tally.AddStageId(stageIndex, run.Stages[stageIndex].StageId);
                    Finish(run, tally, seed, false);

                    return string.Empty;
                }

                if (result.BattleFinish != null)
                    tally.BattlesWon += 1;

                if (stageIndex < run.StageIndex)
                    RecordCleared(run, stageIndex, tally);

                if (result.Kind == RunEngineStepKind.LevelEnd || engine.IsFinished(run))
                {
                    Finish(run, tally, seed, true);

                    return string.Empty;
                }
            }

            return $"Run did not finish in {MaxStepsPerRun} steps.";
        }

        private void RecordCleared(RunDocument run, int stageIndex, BalanceLevelTally tally)
        {
            var stats = tally.Stage(stageIndex);

            stats.Cleared += 1;
            stats.HealthAfter += run.CurrentHealth;
            stats.HealthShareAfter += run.CurrentHealthMaxReference <= 0f ? 0f : run.CurrentHealth / run.CurrentHealthMaxReference;
            stats.ExperienceLevelAfter += run.ExperienceLevel;
            tally.AddStageId(stageIndex, run.Stages[stageIndex].StageId);
        }

        private void Finish(RunDocument run, BalanceLevelTally tally, long seed, bool completed)
        {
            tally.StagesCleared.Add(run.StageIndex);
            tally.FinalExperienceLevel.Add(run.ExperienceLevel);
            tally.PerksPerRun.Add(run.Perks.Count);

            if (completed)
            {
                tally.Completed += 1;

                return;
            }

            if (tally.FailedSeeds.Count < MaxFailedSeeds)
                tally.FailedSeeds.Add(seed);
        }

        private List<int> Pick(RunPendingChoiceDocument choice, BalanceLevelPolicy policy, SeededRandomService random, BalanceLevelTally tally)
        {
            if (string.Equals(choice.Kind, RunPendingChoiceDocument.PerkKind, StringComparison.Ordinal) == false)
                return new List<int> { PickForkButton(policy.ForkPolicy, random) };

            for (int i = 0; i < choice.Options.Count; i++)
                tally.Perk(choice.Options[i]).Offered += 1;

            var picks = PickPerks(choice, policy, random);

            for (int i = 0; i < picks.Count; i++)
                tally.Perk(picks[i]).Picked += 1;

            return picks;
        }

        private int PickForkButton(BalanceForkPolicy forkPolicy, SeededRandomService random)
        {
            if (forkPolicy == BalanceForkPolicy.SecondButton)
                return 2;

            if (forkPolicy == BalanceForkPolicy.Random)
                return random.NextUInt() % 2 == 0 ? 1 : 2;

            return 1;
        }

        private List<int> PickPerks(RunPendingChoiceDocument choice, BalanceLevelPolicy policy, SeededRandomService random)
        {
            var remaining = new List<int>(choice.Options);
            var picks = new List<int>(choice.ChoiceCount);

            if (policy.PerkPolicy == BalancePerkPolicy.Priority)
            {
                for (int i = 0; i < policy.PerkPriority.Count && picks.Count < choice.ChoiceCount; i++)
                {
                    var perkId = policy.PerkPriority[i];

                    if (remaining.Remove(perkId))
                        picks.Add(perkId);
                }
            }

            while (picks.Count < choice.ChoiceCount && 0 < remaining.Count)
            {
                var index = policy.PerkPolicy == BalancePerkPolicy.Random ? (int)(random.NextUInt() % (uint)remaining.Count) : 0;

                picks.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            return picks;
        }

        private BalanceLevelResult CreateResult(int storyLevelId, long firstSeed, int runs, BalanceLevelTally tally)
        {
            var result = new BalanceLevelResult
            {
                StoryLevelId = storyLevelId,
                Runs = runs,
                Completed = tally.Completed,
                CompletionRate = runs == 0 ? 0f : (float)tally.Completed / runs,
                FirstSeed = firstSeed,
                Battles = tally.Battles,
                BattlesWon = tally.BattlesWon,
                StagesCleared = _balanceStatistics.Distribute(tally.StagesCleared),
                FinalExperienceLevel = _balanceStatistics.Distribute(tally.FinalExperienceLevel),
                PerksPerRun = _balanceStatistics.Distribute(tally.PerksPerRun),
                FailedSeeds = tally.FailedSeeds,
            };

            for (int i = 0; i < tally.Stages.Count; i++)
            {
                var stats = tally.Stages[i];

                stats.Reached = stats.Cleared + stats.Deaths;
                stats.DeathRate = stats.Reached == 0 ? 0f : (float)stats.Deaths / stats.Reached;

                if (0 < stats.Cleared)
                {
                    stats.HealthAfter /= stats.Cleared;
                    stats.HealthShareAfter /= stats.Cleared;
                    stats.ExperienceLevelAfter /= stats.Cleared;
                }

                result.Stages.Add(stats);
            }

            foreach (var pair in tally.Perks)
            {
                pair.Value.PickRate = pair.Value.Offered == 0 ? 0f : (float)pair.Value.Picked / pair.Value.Offered;
                result.Perks.Add(pair.Value);
            }

            result.Perks.Sort(ComparePerks);

            return result;
        }

        private int ComparePerks(BalancePerkStats left, BalancePerkStats right)
        {
            var byPicked = right.Picked.CompareTo(left.Picked);

            return byPicked != 0 ? byPicked : left.PerkId.CompareTo(right.PerkId);
        }

        private RunEngine CreateEngine(IConfigDistributor configDistributor)
        {
            var composition = new BattleComposition(configDistributor, new SilentCoreLog());

            return new RunEngine(
                composition.BattleBonusService,
                composition.BattleParameterParser,
                composition.BattleRewardParser,
                composition.BattleScriptDigest,
                composition.BattleSimulationValidator,
                composition.BattleSimulatorService,
                composition.UnitStateBuilder,
                composition.BonusWorkModeParser,
                configDistributor,
                NullLogger<RunEngine>.Instance,
                _rewardApplier,
                _runEventKeys,
                _runRandomFactory,
                _runRecords,
                _runSnapshotBuilder,
                _runStageRoller);
        }
    }
}
