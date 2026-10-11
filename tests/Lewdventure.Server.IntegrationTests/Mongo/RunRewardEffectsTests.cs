using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Runs;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class RunRewardEffectsTests
    {
        private const string UserId = "usr_reward_effects";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const int NextBattlesPerkId = 101;
        private const int HealingPerkId = 102;
        private const int NextBattlesBonusId = 5;
        private const int NextBattlesCount = 5;
        private const int HealingFromMaxBonusId = 9;
        private const float HealingFromMaxValue = 0.5f;

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(CreateVariant(snapshot), "test");

            Assert.That(buildResult.Succeeded, Is.True, string.Join("; ", buildResult.Errors));

            _environment.Services.GetRequiredService<IGameConfigSetProvider>().Swap(buildResult.ConfigSet!);

            await PrepareProfileAsync();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task NextBattlesBonusFromReward_LastsItsOwnBattles()
        {
            var run = await StartWithPerkChoiceAsync(NextBattlesPerkId);

            var chosen = await RunService.ChooseAsync(UserId, run.Id, new List<int> { NextBattlesPerkId }, "next-battles-choice", CancellationToken.None);

            Assert.That(chosen.Succeeded, Is.True, string.Join("; ", chosen.Errors));

            var afterChoice = await RunRepository.GetAsync(run.Id, CancellationToken.None);
            var bonus = FindRunBonus(afterChoice!, NextBattlesBonusId);

            Assert.That(bonus, Is.Not.Null, "бонус next_battles не лёг в забег");
            Assert.That(bonus!.RemainingBattles, Is.EqualTo(NextBattlesCount), "next_battles:N обязан жить N битв, а не весь забег");

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        [Order(2)]
        public async Task HealingBonusFromReward_HealsOnceAndIsNotKept()
        {
            var run = await StartWithPerkChoiceAsync(HealingPerkId);
            var maxHealth = run.CurrentHealth;

            run.CurrentHealth = 100f;
            run.Rev += 1;

            Assert.That(await RunRepository.ReplaceAsync(run, run.Rev - 1, CancellationToken.None), Is.True);

            var chosen = await RunService.ChooseAsync(UserId, run.Id, new List<int> { HealingPerkId }, "healing-choice", CancellationToken.None);

            Assert.That(chosen.Succeeded, Is.True, string.Join("; ", chosen.Errors));

            var afterChoice = await RunRepository.GetAsync(run.Id, CancellationToken.None);
            var expected = MathF.Min(maxHealth, MathF.Round(100f + maxHealth * HealingFromMaxValue, MidpointRounding.AwayFromZero));

            Assert.That(afterChoice!.CurrentHealth, Is.EqualTo(expected).Within(0.001f), "лечение из награды не применилось к ХП забега");
            Assert.That(FindRunBonus(afterChoice, HealingFromMaxBonusId), Is.Null, "лечение не должно оставаться бонусом забега");

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        [Order(3)]
        public async Task MaxHealthChange_RescalesCurrentHealthBeforeBattle()
        {
            var started = await RunService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var run = started.Run!;
            var maxHealth = run.CurrentHealth;

            run.CurrentHealth = maxHealth / 4f;
            run.CurrentHealthMaxReference = maxHealth / 2f;
            run.Rev += 1;

            Assert.That(await RunRepository.ReplaceAsync(run, run.Rev - 1, CancellationToken.None), Is.True);

            var fight = await AdvanceToFightAsync(run.Id);

            Assert.That(fight, Is.Not.Null, "в забеге не дошли до боя");
            Assert.That(fight!.BattleInput!.TeamA.MainUnits[0].CurrentHealth, Is.EqualTo(MathF.Round(maxHealth / 2f, MidpointRounding.AwayFromZero)).Within(0.001f), "ХП не пересчитано по пропорции МАКС.ХП");

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        private RunService RunService => _environment.Services.GetRequiredService<RunService>();

        private RunRepository RunRepository => _environment.Services.GetRequiredService<RunRepository>();

        private async Task<RunStepOutcome?> AdvanceToFightAsync(string runId)
        {
            for (int guard = 0; guard < 50; guard++)
            {
                var refreshed = await RunRepository.GetAsync(runId, CancellationToken.None);

                Assert.That(refreshed, Is.Not.Null);

                if (string.Equals(refreshed!.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                    return null;

                if (refreshed.PendingChoice != null)
                {
                    var picks = refreshed.PendingChoice.Options.GetRange(0, refreshed.PendingChoice.ChoiceCount);

                    await RunService.ChooseAsync(UserId, runId, picks, $"fight-choice-{guard}", CancellationToken.None);

                    continue;
                }

                var step = await RunService.AdvanceAsync(UserId, runId, $"fight-request-{guard}", CancellationToken.None);

                Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));

                if (step.Step?.BattleScript != null)
                    return step.Step;
            }

            return null;
        }

        private async Task<RunDocument> StartWithPerkChoiceAsync(int perkId)
        {
            var started = await RunService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var run = started.Run!;

            run.PendingChoice = new RunPendingChoiceDocument
            {
                Kind = RunPendingChoiceDocument.PerkKind,
                Options = new List<int> { perkId },
                ChoiceCount = 1,
            };
            run.Rev += 1;

            Assert.That(await RunRepository.ReplaceAsync(run, run.Rev - 1, CancellationToken.None), Is.True);

            return run;
        }

        private RunBonusDocument? FindRunBonus(RunDocument run, int bonusId)
        {
            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                if (run.Bonuses[i].BonusId == bonusId)
                    return run.Bonuses[i];
            }

            return null;
        }

        private GameConfigSnapshot CreateVariant(GameConfigSnapshot snapshot)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, "Perks", StringComparison.Ordinal))
                {
                    domains.Add(PatchRows(domain, AddRewardPerks));

                    continue;
                }

                domains.Add(domain);
            }

            var version = _environment.Services.GetRequiredService<ConfigSnapshotHasher>().ComputeVersion(domains);

            return new GameConfigSnapshot(version, snapshot.CreatedAt, snapshot.SourceKind, domains);
        }

        private ConfigSnapshotDomain PatchRows(ConfigSnapshotDomain domain, Action<JArray> patch)
        {
            var rows = JArray.Parse(domain.RowsJson);

            patch(rows);

            return new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rows.ToString(), domain.SourceRows);
        }

        private void AddRewardPerks(JArray rows)
        {
            rows.Add(CreateRewardPerk(rows, NextBattlesPerkId, $"rewards:[bonus:{NextBattlesBonusId}:1]"));
            rows.Add(CreateRewardPerk(rows, HealingPerkId, $"rewards:[bonus:{HealingFromMaxBonusId}:1]"));
        }

        private JObject CreateRewardPerk(JArray rows, int perkId, string parameters)
        {
            var row = (JObject)rows[0].DeepClone();

            row["id"] = perkId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row["perk_type"] = "reward";
            row["perk_parameters"] = parameters;
            row["multiple_picks"] = true;

            return row;
        }

        private async Task PrepareProfileAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = CharacterId, Copies = 0 });
            profile.Loadout.CharacterId = CharacterId;
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }
    }
}
