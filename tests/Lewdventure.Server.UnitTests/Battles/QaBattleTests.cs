using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Logging;
using Server.Shared;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class QaBattleTests
    {
        private const int CharacterId = 1;
        private const int EnemyId = 10101;
        private const int FirePerkId = 2;
        private const int ActionRewardPerkId = 6;
        private const int StoryLevelId = 1;
        private const ulong Seed = 42;

        private readonly List<CoreConfigDomain> _domains = new();

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var serializer = new ConfigSnapshotSerializer(new ConfigSnapshotHasher());
            var source = new FileConfigSnapshotSource(serializer);
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);

            for (int i = 0; i < snapshot.Domains.Count; i++)
                _domains.Add(new CoreConfigDomain(snapshot.Domains[i].Domain, snapshot.Domains[i].RowsJson));
        }

        [Test]
        public void DefaultQa_KeepsTheSameBattle()
        {
            var battleCore = CreateCore(_domains, null);
            var withoutQa = battleCore.Replay(CreateReplayData(CreateHero(), CreateEnemy()));
            var hero = CreateHero();
            var enemy = CreateEnemy();

            hero.Qa = new UnitQaSnapshot();
            enemy.Qa = new UnitQaSnapshot();

            var withDefaultQa = battleCore.Replay(CreateReplayData(hero, enemy));

            Assert.That(battleCore.ComputeDigest(withDefaultQa), Is.EqualTo(battleCore.ComputeDigest(withoutQa)));
        }

        [Test]
        public void HeroStrikeMultiplierZero_HeroStrikesDealNoDamage()
        {
            var hero = CreateHero();

            hero.Qa = new UnitQaSnapshot { StrikeDamageMultiplier = 0f };

            var script = CreateCore(_domains, null).Replay(CreateReplayData(hero, CreateEnemy()));
            var heroDamage = SumDamageBy(script, CharacterId);
            var enemyDamage = SumDamageBy(script, EnemyId);

            Assert.That(CountDamageBy(script, CharacterId), Is.GreaterThan(0), "герой ни разу не ударил, тест ничего не проверяет");
            Assert.That(heroDamage, Is.EqualTo(0f));
            Assert.That(enemyDamage, Is.GreaterThan(0f));
            Assert.That(script.OutcomeType, Is.Not.EqualTo(OutcomeType.TeamAWin));
        }

        [Test]
        public void EnemyHealthMultiplier_RaisesMaxHealth()
        {
            var battleCore = CreateCore(_domains, null);
            var enemy = CreateEnemy();
            var baseHealth = battleCore.BuildCharacteristics(enemy, BattleSide.Defending, false, StoryLevelId, 0).MaxHealth;

            enemy.Qa = new UnitQaSnapshot { HealthMultiplier = 10f };

            var characteristics = battleCore.BuildCharacteristics(enemy, BattleSide.Defending, false, StoryLevelId, 0);

            Assert.That(characteristics.MaxHealth, Is.EqualTo(baseHealth * 10f).Within(0.01f));
            Assert.That(characteristics.Health, Is.EqualTo(characteristics.MaxHealth).Within(0.01f));
        }

        [Test]
        public void StartHealth_SetsHealthAtBattleStart()
        {
            var hero = CreateHero();

            hero.Qa = new UnitQaSnapshot { StartHealth = 1f };

            var characteristics = CreateCore(_domains, null).BuildCharacteristics(hero, BattleSide.Attacking, false, StoryLevelId, 0);

            Assert.That(characteristics.Health, Is.EqualTo(1f));
        }

        [Test]
        public void EnemyKillAtStart_WinsWithoutTurns()
        {
            var enemy = CreateEnemy();

            enemy.Qa = new UnitQaSnapshot { KillAtStart = true };

            var script = CreateCore(_domains, null).Replay(CreateReplayData(CreateHero(), enemy));

            Assert.That(script.OutcomeType, Is.EqualTo(OutcomeType.TeamAWin));
            Assert.That(CountCommands(script, CommandType.KillUnit, EnemyId), Is.EqualTo(1));
            Assert.That(CountDamageBy(script, CharacterId), Is.EqualTo(0));
        }

        [Test]
        public void HeroKillAtStart_LosesWithoutTurns()
        {
            var hero = CreateHero();

            hero.Qa = new UnitQaSnapshot { KillAtStart = true };

            var script = CreateCore(_domains, null).Replay(CreateReplayData(hero, CreateEnemy()));

            Assert.That(script.OutcomeType, Is.EqualTo(OutcomeType.TeamBWin));
            Assert.That(CountCommands(script, CommandType.KillUnit, CharacterId), Is.EqualTo(1));
            Assert.That(CountDamageBy(script, EnemyId), Is.EqualTo(0));
        }

        [Test]
        public void ActionRewardProgress_TriggersBeforeTheThreshold()
        {
            var patched = ReplaceDomain(_domains, "Perks", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if (row["id"]!.ToString() == ActionRewardPerkId.ToString())
                        row["perk_parameters"] = "rewards_on_action:[bonus:1:1];\nactions:[attack:100];\nrewards_chance:[1]";
                }
            });
            var battleCore = CreateCore(patched, null);
            var hero = CreateHero();

            hero.ActivePerkIds.Add(ActionRewardPerkId);

            var withoutProgress = battleCore.Replay(CreateReplayData(hero, CreateEnemy()));

            hero.Qa = new UnitQaSnapshot { ActionRewardProgress = 99 };

            var withProgress = battleCore.Replay(CreateReplayData(hero, CreateEnemy()));

            Assert.That(CountCommands(withoutProgress, CommandType.TriggerPerk, CharacterId), Is.EqualTo(0));
            Assert.That(CountCommands(withProgress, CommandType.TriggerPerk, CharacterId), Is.GreaterThan(0));
        }

        [Test]
        public void FormulaLog_HasOneFormulaPerDamageCommand()
        {
            var formulaLog = new BattleFormulaLog();
            var hero = CreateHero();

            hero.ActivePerkIds.Add(FirePerkId);

            var script = CreateCore(_domains, formulaLog).Replay(CreateReplayData(hero, CreateEnemy()));
            var damages = CollectDamageCommands(script);
            var kinds = new HashSet<string>();

            Assert.That(formulaLog.Formulas.Count, Is.EqualTo(damages.Count));

            for (int i = 0; i < formulaLog.Formulas.Count; i++)
            {
                var formula = formulaLog.Formulas[i];
                var index = FindDamage(damages, formula);

                Assert.That(index, Is.GreaterThanOrEqualTo(0), $"формула {formula.Kind} {formula.ActorId} → {formula.TargetId} = {formula.Result} без команды урона");

                damages.RemoveAt(index);
                kinds.Add(formula.Kind);
            }

            Assert.That(kinds, Does.Contain(BattleFormulaKinds.Attack));
            Assert.That(kinds, Does.Contain(BattleFormulaKinds.Perk));
        }

        [Test]
        public void FormulaLog_DoesNotChangeTheBattle()
        {
            var plain = CreateCore(_domains, null);
            var recorded = CreateCore(_domains, new BattleFormulaLog());
            var hero = CreateHero();

            hero.ActivePerkIds.Add(FirePerkId);

            var plainScript = plain.Replay(CreateReplayData(hero, CreateEnemy()));
            var recordedScript = recorded.Replay(CreateReplayData(hero, CreateEnemy()));

            Assert.That(recorded.ComputeDigest(recordedScript), Is.EqualTo(plain.ComputeDigest(plainScript)));
        }

        [Test]
        public void QaInBattleInputJson_RoundTripsToTheSameBattle()
        {
            var battleCore = CreateCore(_domains, null);
            var hero = CreateHero();
            var enemy = CreateEnemy();

            hero.Qa = new UnitQaSnapshot { StrikeDamageMultiplier = 0f, ActionRewardProgress = 3 };
            enemy.Qa = new UnitQaSnapshot { HealthMultiplier = 2f, StartHealth = 100f };

            var data = CreateReplayData(hero, enemy);
            var settings = new BattleJsonSettingsFactory().Create();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data, settings);
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleReplayData>(json, settings)!;

            Assert.That(json, Does.Contain("\"qa\""));
            Assert.That(restored.TeamB.MainUnits[0].Qa!.HealthMultiplier, Is.EqualTo(2f));
            Assert.That(battleCore.ComputeDigest(battleCore.Replay(restored)), Is.EqualTo(battleCore.ComputeDigest(battleCore.Replay(data))));
        }

        [Test]
        public void NoQa_IsNotWrittenToBattleInputJson()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(CreateReplayData(CreateHero(), CreateEnemy()), new BattleJsonSettingsFactory().Create());

            Assert.That(json, Does.Not.Contain("\"qa\""));
        }

        [TestCase(-1f, 1f, 0f, 0)]
        [TestCase(1f, 0f, 0f, 0)]
        [TestCase(1f, 1f, -1f, 0)]
        [TestCase(1f, 1f, 0f, -1)]
        [TestCase(float.NaN, 1f, 0f, 0)]
        public void InvalidQa_IsRejected(float strikeDamageMultiplier, float healthMultiplier, float startHealth, int actionRewardProgress)
        {
            var hero = CreateHero();

            hero.Qa = new UnitQaSnapshot
            {
                StrikeDamageMultiplier = strikeDamageMultiplier,
                HealthMultiplier = healthMultiplier,
                StartHealth = startHealth,
                ActionRewardProgress = actionRewardProgress,
            };

            var isValid = CreateCore(_domains, null).TryValidate(CreateReplayData(hero, CreateEnemy()), out var errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("qa"));
        }

        private int FindDamage(List<(int ActorId, int TargetId, float Damage)> damages, BattleFormula formula)
        {
            for (int i = 0; i < damages.Count; i++)
            {
                if (damages[i].ActorId == formula.ActorId && damages[i].TargetId == formula.TargetId && damages[i].Damage == formula.Result)
                    return i;
            }

            return -1;
        }

        private List<(int ActorId, int TargetId, float Damage)> CollectDamageCommands(IBattleScriptResponse script)
        {
            var damages = new List<(int ActorId, int TargetId, float Damage)>();

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.ShowDamage)
                        continue;

                    damages.Add((command.Parameters["actorId"]!.Value<int>(), command.Parameters["targetId"]!.Value<int>(), command.Parameters["damage"]!.Value<float>()));
                }
            }

            return damages;
        }

        private float SumDamageBy(IBattleScriptResponse script, int actorId)
        {
            var damages = CollectDamageCommands(script);
            var sum = 0f;

            for (int i = 0; i < damages.Count; i++)
            {
                if (damages[i].ActorId == actorId && damages[i].TargetId != actorId)
                    sum += damages[i].Damage;
            }

            return sum;
        }

        private int CountDamageBy(IBattleScriptResponse script, int actorId)
        {
            var damages = CollectDamageCommands(script);
            var count = 0;

            for (int i = 0; i < damages.Count; i++)
            {
                if (damages[i].ActorId == actorId && damages[i].TargetId != actorId)
                    count += 1;
            }

            return count;
        }

        private int CountCommands(IBattleScriptResponse script, CommandType commandType, int unitId)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != commandType)
                        continue;

                    var unitToken = command.Parameters["unitId"] ?? command.Parameters["actorId"];

                    if (unitToken != null && unitToken.Value<int>() == unitId)
                        count += 1;
                }
            }

            return count;
        }

        private IBattleCore CreateCore(IReadOnlyList<CoreConfigDomain> domains, IBattleFormulaRecorder? battleFormulaRecorder)
        {
            var result = new SharedCoreFactory().CreateFromDomains(domains, new SilentCoreLog(), null, battleFormulaRecorder);

            Assert.That(result.Errors, Is.Empty);

            return result.SharedCore!.Battle;
        }

        private List<CoreConfigDomain> ReplaceDomain(IReadOnlyList<CoreConfigDomain> domains, string domain, Action<JArray> patch)
        {
            var patched = new List<CoreConfigDomain>(domains.Count);

            for (int i = 0; i < domains.Count; i++)
            {
                if (string.Equals(domains[i].Domain, domain, StringComparison.Ordinal) == false)
                {
                    patched.Add(domains[i]);

                    continue;
                }

                var rows = JArray.Parse(domains[i].RowsJson);

                patch(rows);
                patched.Add(new CoreConfigDomain(domain, rows.ToString()));
            }

            return patched;
        }

        private UnitSnapshot CreateHero()
        {
            return new UnitSnapshot
            {
                Id = CharacterId,
                Level = 1,
                SlotIndex = 0,
            };
        }

        private UnitSnapshot CreateEnemy()
        {
            return new UnitSnapshot
            {
                Id = EnemyId,
                Level = 1,
                SlotIndex = 0,
            };
        }

        private BattleReplayData CreateReplayData(UnitSnapshot hero, UnitSnapshot enemy)
        {
            var teamA = new TeamSnapshot();
            var teamB = new TeamSnapshot();

            teamA.MainUnits.Add(hero);
            teamB.MainUnits.Add(enemy);

            return new BattleReplayData
            {
                TeamA = teamA,
                TeamB = teamB,
                StoryLevelId = StoryLevelId,
                Seed = Seed,
            };
        }
    }
}
