using Newtonsoft.Json.Linq;
using Server.Bonuses;
using Server.Dialogues;
using Server.GameConfigs;
using Server.Logging;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class DialogueConfigTests
    {
        private readonly ConfigDomainNames _domainNames = new();

        [Test]
        public async Task Fixture_ParsesDialoguesFromDesignerSample()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, _ => { });

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));

            var dialogues = result.ConfigSet!.Distributor.Dialogues;

            Assert.That(dialogues.TryGet(1, 4, out var choiceStep), Is.True);
            Assert.That(choiceStep!.NextSteps, Is.EqualTo(new[] { 5, 8 }));
            Assert.That(choiceStep.LocalizationKeys, Has.Length.EqualTo(3));
            Assert.That(choiceStep.Characters, Has.Length.EqualTo(2));
            Assert.That(choiceStep.Characters[1].SpriteName, Is.EqualTo("character_2_dialogue_sprite"));
            Assert.That(choiceStep.Characters[1].ScreenPercent, Is.EqualTo(66f));

            Assert.That(dialogues.TryGet(2, 1, out var multilineStep), Is.True);
            Assert.That(multilineStep!.Characters, Has.Length.EqualTo(3));
            Assert.That(multilineStep.Characters[2].SpriteName, Is.EqualTo("summon_2_dialogue_sprite"));
            Assert.That(multilineStep.IsFadeOnStart, Is.True);
            Assert.That(multilineStep.Backgrounds, Is.EqualTo(new[] { "cave_background" }));
            Assert.That(multilineStep.TextSpeed, Is.EqualTo(25f));

            Assert.That(dialogues.TryGet(3, 5, out var endStep), Is.True);
            Assert.That(endStep!.NextSteps, Is.Empty);
            Assert.That(endStep.Characters, Is.Empty);
            Assert.That(endStep.CurrentCharacterLocName, Is.Empty);
        }

        [Test]
        public async Task Sounds_KeepEmptySlotsInBackgroundOrder()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, rows =>
            {
                ((JObject)rows[0])["backgrounds"] = "cave_intro;cave_loop";
                ((JObject)rows[0])["sounds"] = "wind;";
            });

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.ConfigSet!.Distributor.Dialogues.TryGet(1, 1, out var step), Is.True);
            Assert.That(step!.Sounds, Is.EqualTo(new[] { "wind", string.Empty }));
        }

        [Test]
        public async Task NextStepToMissingStep_IsError()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, rows => ((JObject)rows[0])["next_step"] = "99");

            Assert.That(result.Errors, Has.Some.Contains("диалог 1 шаг 1: next_step ведёт на шаг 99"));
        }

        [Test]
        public async Task ChoiceCountMismatch_IsError()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, rows => ((JObject)rows[3])["next_step"] = "5");

            Assert.That(result.Errors, Has.Some.Contains("диалог 1 шаг 4: вариантов выбора 2, а переходов в next_step 1"));
        }

        [Test]
        public async Task HubWithoutExit_IsError()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if (row["id"]!.ToString() == "3" && row["step"]!.ToString() == "1")
                        row["next_step"] = "2;3;4;2";
                }
            });

            Assert.That(result.Errors, Has.Some.Contains("диалог 3 шаг 2: из шага нельзя дойти до конца диалога"));
        }

        [Test]
        public async Task HubWithExit_IsAllowed()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.Dialogues, _ => { });

            Assert.That(result.Errors, Has.None.Contains("Dialogues"));
        }

        [Test]
        public async Task StoryLevelDialogues_ParseTriggers()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.StoryLevels, rows =>
            {
                ((JObject)rows[0])["dialogues_ids"] = "1;2;3";
                ((JObject)rows[0])["dialogues_triggers"] = "on_start;on_loss;on_stage_entered:2";
            });

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));

            var level = result.ConfigSet!.Distributor.StoryLevels.Collection[0];

            Assert.That(level.DialogueIds, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(level.DialogueTriggers[0].Type, Is.EqualTo(DialogueTriggerType.OnStart));
            Assert.That(level.DialogueTriggers[1].Type, Is.EqualTo(DialogueTriggerType.OnLoss));
            Assert.That(level.DialogueTriggers[2].Type, Is.EqualTo(DialogueTriggerType.OnStageEntered));
            Assert.That(level.DialogueTriggers[2].StageNumber, Is.EqualTo(2));
        }

        [Test]
        public async Task StoryLevelDialogues_ReportBrokenLinks()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.StoryLevels, rows =>
            {
                ((JObject)rows[0])["dialogues_ids"] = "1;77;2;3";
                ((JObject)rows[0])["dialogues_triggers"] = "on_start;on_win;on_retreat;on_stage_entered:99";
            });

            Assert.That(result.Errors, Has.Some.Contains("диалога 77 нет в листе Dialogues"));
            Assert.That(result.Errors, Has.Some.Contains("триггер «on_retreat» не распознан"));
            Assert.That(result.Errors, Has.Some.Contains("«on_stage_entered:99» — этапа 99 нет"));
        }

        [Test]
        public async Task StoryLevelDialogues_LengthMismatch_IsError()
        {
            var result = await BuildPatchedAsync(ConfigDomainNames.StoryLevels, rows =>
            {
                ((JObject)rows[0])["dialogues_ids"] = "1;2";
                ((JObject)rows[0])["dialogues_triggers"] = "on_start";
            });

            Assert.That(result.Errors, Has.Some.Contains("диалогов в dialogues_ids 2, а триггеров в dialogues_triggers 1"));
        }

        private async Task<GameConfigBuildResult> BuildPatchedAsync(string domainName, Action<JArray> patch)
        {
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(new ConfigSnapshotHasher()));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, domainName, StringComparison.Ordinal))
                {
                    var rows = JArray.Parse(domain.RowsJson);

                    patch(rows);
                    domain = new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rows.ToString(), domain.SourceRows);
                }

                domains.Add(domain);
            }

            return CreateBuilder().Build(new GameConfigSnapshot(snapshot.Version, snapshot.CreatedAt, snapshot.SourceKind, domains), "test");
        }

        private GameConfigSetBuilder CreateBuilder()
        {
            return new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(_domainNames, new DialogueDataValidator(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new ProgressionDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());
        }
    }
}
