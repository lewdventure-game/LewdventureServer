using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Logging;
using Server.Shared;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class SkillEngineTests
    {
        private const int CharacterId = 1;
        private const int EnemyId = 10101;
        private const int StoryLevelId = 1;
        private const ulong Seed = 42;

        private const string EnergyTriggers = "energy_needed:[energy:{30}]";

        private const string EnergyActions = "damage:[damage:{1.1,1.2},target:{3,3},is_spell_amp:{0,0},timing:{0.5,0.5}];set_bonus:[bonus_id:{1,1},target:{0,0},timing:{0.2,0.2}]";

        private readonly List<CoreConfigDomain> _domains = new();

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(new ConfigSnapshotHasher()));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);

            for (int i = 0; i < snapshot.Domains.Count; i++)
                _domains.Add(new CoreConfigDomain(snapshot.Domains[i].Domain, snapshot.Domains[i].RowsJson));
        }

        [Test]
        public void EnergySkill_WhenEnergyIsEnough_DealsDamageAndGrantsBonus()
        {
            var battleCore = CreateCore(WithSkill(EnergyTriggers, EnergyActions));
            var script = battleCore.Replay(CreateReplayData(CreateUnitSnapshot()));
            var cast = FindCommand(script, CommandType.CastSkill);

            Assert.That(cast, Is.Not.Null, "скилл за энергию не сработал");

            var characteristics = battleCore.BuildCharacteristics(CreateUnitSnapshot(), BattleSide.Attacking, false, StoryLevelId, 0);
            var expectedDamage = MathF.Round(characteristics.Damage * 1.1f, MidpointRounding.AwayFromZero);
            var skillDamage = FindSkillDamage(script, expectedDamage);

            Assert.That(skillDamage, Is.Not.Null, $"не нашёл урон скилла {expectedDamage}");
            Assert.That(FindCommand(script, CommandType.SetBonus), Is.Not.Null, "set_bonus не применился");
            Assert.That(FindCommand(script, CommandType.SetEnergy), Is.Not.Null, "энергия не обнулилась");
        }

        [Test]
        public void EnergySkill_DamageIgnoresDefence()
        {
            var battleCore = CreateCore(WithSkill(EnergyTriggers, EnergyActions));
            var unitSnapshot = CreateUnitSnapshot();
            var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, StoryLevelId, 0);
            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var withoutDefence = MathF.Round(characteristics.Damage * 1.1f, MidpointRounding.AwayFromZero);

            Assert.That(FindSkillDamage(script, withoutDefence), Is.Not.Null, "урон скилла обрезан защитой цели");
        }

        [Test]
        public void SkillWithoutSatisfiedTrigger_DoesNotActivate()
        {
            var battleCore = CreateCore(WithSkill("energy_needed:[energy:{100000}]", EnergyActions));
            var script = battleCore.Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(FindCommand(script, CommandType.CastSkill), Is.Null, "скилл сработал без выполненного условия");
        }

        [Test]
        public void CooldownSkill_ActivatesFromBattleStart()
        {
            var triggers = "on_cooldown:[cooldown:{2},is_start_availiable:{1}]";
            var actions = "damage:[damage:{0.5},target:{1},is_spell_amp:{0},timing:{0.1}]";
            var battleCore = CreateCore(WithSkill(triggers, actions));
            var script = battleCore.Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(CountCommands(script, CommandType.CastSkill), Is.GreaterThan(0), "скилл по кулдауну не сработал");
        }

        [Test]
        public void CooldownSkill_WithoutStartAvailability_WaitsForCooldown()
        {
            var available = "on_cooldown:[cooldown:{3},is_start_availiable:{1}]";
            var delayed = "on_cooldown:[cooldown:{3},is_start_availiable:{0}]";
            var actions = "damage:[damage:{0.5},target:{1},is_spell_amp:{0},timing:{0.1}]";
            var availableScript = CreateCore(WithSkill(available, actions)).Replay(CreateReplayData(CreateUnitSnapshot()));
            var delayedScript = CreateCore(WithSkill(delayed, actions)).Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(CountCommands(delayedScript, CommandType.CastSkill), Is.LessThan(CountCommands(availableScript, CommandType.CastSkill)));
        }

        [Test]
        public void InstantSkill_ActivatesOutOfTurnOnLowHealth()
        {
            var triggers = "ally_health_lower:[health:{0.95},is_instant_activation:{1}]";
            var actions = "set_bonus:[bonus_id:{1},target:{0},timing:{0.1}]";
            var battleCore = CreateCore(WithSkill(triggers, actions));
            var script = battleCore.Replay(CreateReplayData(CreateUnitSnapshot()));
            var castStepIndex = FindFirstStepIndex(script, CommandType.CastSkill);
            var counterStepIndex = FindFirstStepIndex(script, CommandType.ShowDamage);

            Assert.That(castStepIndex, Is.GreaterThanOrEqualTo(0), "внеочередной скилл не сработал");
            Assert.That(counterStepIndex, Is.LessThan(castStepIndex), "внеочередной скилл сработал до первого урона");
        }

        [Test]
        public void SkillLevel_FollowsPromoteToSkillLevels()
        {
            var resolver = new SkillLevelResolver();
            var promoteToSkillLevels = new[] { 0, 1, 3, 5 };

            Assert.That(resolver.Resolve(promoteToSkillLevels, 0), Is.EqualTo(0));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 1), Is.EqualTo(1));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 2), Is.EqualTo(1));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 3), Is.EqualTo(2));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 9), Is.EqualTo(3));
        }

        [Test]
        public void SkillLevel_PicksArgumentsOfThatLevel()
        {
            var battleCore = CreateCore(WithSkill(EnergyTriggers, EnergyActions, promoteToSkillLevels: "0;1"));
            var baseSnapshot = CreateUnitSnapshot();
            var promotedSnapshot = CreateUnitSnapshot();

            promotedSnapshot.Level = 2;

            var characteristics = battleCore.BuildCharacteristics(baseSnapshot, BattleSide.Attacking, false, StoryLevelId, 0);
            var baseScript = battleCore.Replay(CreateReplayData(baseSnapshot));
            var promotedScript = battleCore.Replay(CreateReplayData(promotedSnapshot));

            Assert.That(FindSkillDamage(baseScript, MathF.Round(characteristics.Damage * 1.1f, MidpointRounding.AwayFromZero)), Is.Not.Null);
            Assert.That(FindSkillDamage(promotedScript, MathF.Round(characteristics.Damage * 1.2f, MidpointRounding.AwayFromZero)), Is.Not.Null);
        }

        private List<CoreConfigDomain> WithSkill(string triggers, string actions, string promoteToSkillLevels = "0")
        {
            var patched = ReplaceDomain(_domains, "Skills", rows =>
            {
                rows.Clear();
                rows.Add(new JObject
                {
                    ["id"] = "1",
                    ["triggers"] = triggers,
                    ["actions"] = actions,
                });
            });

            return ReplaceDomain(patched, "Characters", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = "1";
                row["promote_to_skill_levels"] = promoteToSkillLevels;
            });
        }

        private IBattleCore CreateCore(IReadOnlyList<CoreConfigDomain> domains)
        {
            var result = new SharedCoreFactory().CreateFromDomains(domains, new SilentCoreLog());

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

        private UnitSnapshot CreateUnitSnapshot()
        {
            return new UnitSnapshot
            {
                Id = CharacterId,
                Level = 1,
                SlotIndex = 0,
            };
        }

        private BattleReplayData CreateReplayData(UnitSnapshot unitSnapshot)
        {
            var teamA = new TeamSnapshot();

            teamA.MainUnits.Add(unitSnapshot);

            var teamB = new TeamSnapshot();

            teamB.MainUnits.Add(new UnitSnapshot
            {
                Id = EnemyId,
                Level = 1,
                SlotIndex = 0,
            });

            return new BattleReplayData
            {
                TeamA = teamA,
                TeamB = teamB,
                StoryLevelId = StoryLevelId,
                Seed = Seed,
            };
        }

        private BattleCommand? FindCommand(IBattleScriptResponse script, CommandType commandType)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType == commandType)
                        return commands[commandIndex];
                }
            }

            return null;
        }

        private int CountCommands(IBattleScriptResponse script, CommandType commandType)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType == commandType)
                        count += 1;
                }
            }

            return count;
        }

        private int FindFirstStepIndex(IBattleScriptResponse script, CommandType commandType)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType == commandType)
                        return i;
                }
            }

            return -1;
        }

        private BattleCommand? FindSkillDamage(IBattleScriptResponse script, float expectedDamage)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                if (script.Steps[i].Phase != BattlePhaseType.UnitSkill && script.Steps[i].Phase != BattlePhaseType.EnergySkill)
                    continue;

                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.ShowDamage)
                        continue;

                    if (MathF.Abs(command.Parameters["damage"]!.Value<float>() - expectedDamage) <= 0.001f)
                        return command;
                }
            }

            return null;
        }
    }
}
