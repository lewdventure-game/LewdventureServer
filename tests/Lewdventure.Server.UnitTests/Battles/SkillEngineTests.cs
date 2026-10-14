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
        public void EnergySkill_StartsWithUnitsCooldown()
        {
            var script = CreateCore(WithSkill(EnergyTriggers, EnergyActions)).Replay(CreateReplayData(CreateUnitSnapshot()));
            var castIndex = FindFirstCommandOf(script, CommandType.CastSkill, "actorId", CharacterId);

            Assert.That(castIndex, Is.GreaterThan(0));

            var previous = script.Steps[castIndex - 1];

            Assert.That(previous.Phase, Is.EqualTo(BattlePhaseType.EnergySkill));
            Assert.That(previous.Commands.Count, Is.EqualTo(1));
            Assert.That(previous.Commands[0].CommandType, Is.EqualTo(CommandType.Wait));
            Assert.That(previous.Commands[0].Parameters["seconds"]!.Value<float>(), Is.GreaterThan(0f));
        }

        [Test]
        public void UnitSkillThenAttack_HasSingleUnitsCooldown()
        {
            var triggers = "on_cooldown:[cooldown:{1},is_start_available:{1}]";
            var actions = "damage:[damage:{0.5},target:{1},is_spell_amp:{0},timing:{0.1}]";
            var script = CreateCore(WithSkill(triggers, actions)).Replay(CreateReplayData(CreateUnitSnapshot()));
            var castIndex = FindFirstCommandOf(script, CommandType.CastSkill, "actorId", CharacterId);
            var waitsBeforeAttack = 0;
            var attackFound = false;

            for (int i = castIndex + 1; i < script.Steps.Count && attackFound == false; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType == CommandType.PlayAnimation && command.Parameters["animationKey"]!.ToString() == "attack")
                    {
                        attackFound = true;

                        break;
                    }

                    if (command.CommandType == CommandType.Wait)
                        waitsBeforeAttack += 1;
                }
            }

            Assert.That(castIndex, Is.GreaterThanOrEqualTo(0), "скилл не сработал");
            Assert.That(attackFound, Is.True);
            Assert.That(waitsBeforeAttack, Is.EqualTo(1), "между скиллами и атакой должна быть одна пауза units_cooldown");
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
        public void CooldownSkill_CorrectStartAvailableSpelling_MatchesLegacySpelling()
        {
            var legacy = "on_cooldown:[cooldown:{3},is_start_availiable:{1}]";
            var correct = "on_cooldown:[cooldown:{3},is_start_available:{1}]";
            var actions = "damage:[damage:{0.5},target:{1},is_spell_amp:{0},timing:{0.1}]";
            var legacyScript = CreateCore(WithSkill(legacy, actions)).Replay(CreateReplayData(CreateUnitSnapshot()));
            var correctScript = CreateCore(WithSkill(correct, actions)).Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(CountCommands(correctScript, CommandType.CastSkill), Is.EqualTo(CountCommands(legacyScript, CommandType.CastSkill)));
            Assert.That(FindFirstStepIndex(correctScript, CommandType.CastSkill), Is.EqualTo(FindFirstStepIndex(legacyScript, CommandType.CastSkill)));
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
            var promoteToSkillLevels = new[] { 1, 2, 4, 6 };

            Assert.That(resolver.Resolve(promoteToSkillLevels, 1), Is.EqualTo(0));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 2), Is.EqualTo(1));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 3), Is.EqualTo(1));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 4), Is.EqualTo(2));
            Assert.That(resolver.Resolve(promoteToSkillLevels, 9), Is.EqualTo(3));
        }

        [Test]
        public void SkillLevel_PicksArgumentsOfThatLevel()
        {
            var battleCore = CreateCore(WithSkill(EnergyTriggers, EnergyActions, promoteToSkillLevels: "1;2"));
            var baseSnapshot = CreateUnitSnapshot();
            var promotedSnapshot = CreateUnitSnapshot();

            promotedSnapshot.Level = 2;

            var characteristics = battleCore.BuildCharacteristics(baseSnapshot, BattleSide.Attacking, false, StoryLevelId, 0);
            var baseScript = battleCore.Replay(CreateReplayData(baseSnapshot));
            var promotedScript = battleCore.Replay(CreateReplayData(promotedSnapshot));

            Assert.That(FindSkillDamage(baseScript, MathF.Round(characteristics.Damage * 1.1f, MidpointRounding.AwayFromZero)), Is.Not.Null);
            Assert.That(FindSkillDamage(promotedScript, MathF.Round(characteristics.Damage * 1.2f, MidpointRounding.AwayFromZero)), Is.Not.Null);
        }

        [Test]
        public void SummonSkill_UsesItsOwnLevel()
        {
            var battleCore = CreateCore(WithSummonSkill("1"));
            var baseSummon = CreateSummonSnapshot(1, 1);
            var upgradedSummon = CreateSummonSnapshot(1, 2);
            var summonDamage = battleCore.BuildCharacteristics(baseSummon, BattleSide.Attacking, true, StoryLevelId, 0).Damage;
            var baseScript = battleCore.Replay(CreateReplayData(CreateUnitSnapshot(), baseSummon));
            var upgradedScript = battleCore.Replay(CreateReplayData(CreateUnitSnapshot(), upgradedSummon));

            Assert.That(FindPhaseDamage(baseScript, BattlePhaseType.SummonSkill, MathF.Round(summonDamage * 0.5f, MidpointRounding.AwayFromZero)), Is.Not.Null, "скилл саммона первого уровня");
            Assert.That(FindPhaseDamage(upgradedScript, BattlePhaseType.SummonSkill, MathF.Round(summonDamage * 0.9f, MidpointRounding.AwayFromZero)), Is.Not.Null, "скилл саммона второго уровня");
        }

        [Test]
        public void SummonInstantSkill_ActivatesOutOfTurnOnOwnerLowHealth()
        {
            var patched = ReplaceDomain(WithSummonSkill("1"), "Skills", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if (row["id"]!.ToString() != "50")
                        continue;

                    row["triggers"] = "ally_health_lower:[health:{0.95:0.95},is_instant_activation:{1:1}]";
                    row["actions"] = "set_bonus:[bonus_id:{1:1},target:{0:0},timing:{0.1:0.1}]";
                }
            });

            var script = CreateCore(patched).Replay(CreateReplayData(CreateUnitSnapshot(), CreateSummonSnapshot(1, 1)));
            var firstHitIndex = FindFirstCommandOf(script, CommandType.ShowDamage, "targetId", CharacterId);
            var castStepIndex = -1;

            for (int i = 0; i < script.Steps.Count && castStepIndex < 0; i++)
            {
                if (script.Steps[i].Phase == BattlePhaseType.SummonSkill && FindCommandInStep(script.Steps[i], CommandType.CastSkill))
                    castStepIndex = i;
            }

            Assert.That(firstHitIndex, Is.GreaterThanOrEqualTo(0), "персонаж не получил урона, тест ничего не проверяет");
            Assert.That(castStepIndex, Is.GreaterThan(firstHitIndex), "скилл саммона не сработал после урона по персонажу");
            Assert.That(script.Steps[castStepIndex].Turn, Is.EqualTo(script.Steps[firstHitIndex].Turn), "скилл саммона ждал своей фазы вместо внеочередного каста");
        }

        private bool FindCommandInStep(BattleStep step, CommandType commandType)
        {
            for (int i = 0; i < step.Commands.Count; i++)
            {
                if (step.Commands[i].CommandType == commandType)
                    return true;
            }

            return false;
        }

        [Test]
        public void SummonMaxHealthBonus_KeepsAbsoluteSnapshotHealth()
        {
            var patched = ReplaceDomain(_domains, "Bonuses", rows =>
            {
                rows.Add(new JObject
                {
                    ["id"] = "90",
                    ["operator"] = "add",
                    ["bonus_type"] = "max_health_perk",
                    ["bonus_value"] = "0.2",
                    ["work_modes"] = "if_equipped:summons:1",
                });
            });

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                ((JObject)rows[0])["skill_ids"] = string.Empty;
            });

            var battleCore = CreateCore(patched);
            var snapshot = CreateUnitSnapshot();

            snapshot.CurrentHealth = 300f;
            snapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 90, Count = 1, RemainingBattles = 0 });

            var script = battleCore.Replay(CreateReplayData(snapshot, CreateSummonSnapshot(1, 1)));
            var startHealth = ReadHealthBeforeFirstHit(script, CharacterId);

            Assert.That(startHealth, Is.EqualTo(300f), "бонус саммона к МАКС.ХП пересчитал принесённое ХП пропорционально");
        }

        [Test]
        public void SummonMaxHealthBonus_FullSnapshotHealthIsFullMaximum()
        {
            var patched = ReplaceDomain(_domains, "Bonuses", rows =>
            {
                rows.Add(new JObject
                {
                    ["id"] = "90",
                    ["operator"] = "add",
                    ["bonus_type"] = "max_health_perk",
                    ["bonus_value"] = "0.2",
                    ["work_modes"] = "if_equipped:summons:1",
                });
            });

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                ((JObject)rows[0])["skill_ids"] = string.Empty;
            });

            var battleCore = CreateCore(patched);
            var snapshot = CreateUnitSnapshot();
            var withoutSummon = battleCore.BuildCharacteristics(snapshot, BattleSide.Attacking, false, StoryLevelId, 0).MaxHealth;

            snapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 90, Count = 1, RemainingBattles = 0 });

            var script = battleCore.Replay(CreateReplayData(snapshot, CreateSummonSnapshot(1, 1)));
            var startHealth = ReadHealthBeforeFirstHit(script, CharacterId);

            Assert.That(startHealth, Is.EqualTo(MathF.Round(withoutSummon * 1.2f, MidpointRounding.AwayFromZero)), "с полным ХП бой начинается с полным максимумом вместе с бонусом саммона");
        }

        private float ReadHealthBeforeFirstHit(IBattleScriptResponse script, int unitId)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.ShowDamage || command.Parameters["targetId"]!.Value<int>() != unitId)
                        continue;

                    var damage = command.Parameters["damage"]!.Value<float>();

                    for (int next = commandIndex + 1; next < commands.Count; next++)
                    {
                        var hpCommand = commands[next];

                        if (hpCommand.CommandType == CommandType.SetHp && hpCommand.Parameters["unitId"]!.Value<int>() == unitId)
                            return hpCommand.Parameters["hp"]!.Value<float>() + damage;
                    }
                }
            }

            return -1f;
        }

        [Test]
        public void SummonSkill_LockedByMastery_DoesNotActivate()
        {
            var battleCore = CreateCore(WithSummonSkill("3"));
            var lockedScript = battleCore.Replay(CreateReplayData(CreateUnitSnapshot(), CreateSummonSnapshot(2, 1)));
            var unlockedScript = battleCore.Replay(CreateReplayData(CreateUnitSnapshot(), CreateSummonSnapshot(3, 1)));

            Assert.That(CountPhaseCommands(lockedScript, BattlePhaseType.SummonSkill, CommandType.CastSkill), Is.EqualTo(0), "скилл открылся до нужного мастерства");
            Assert.That(CountPhaseCommands(unlockedScript, BattlePhaseType.SummonSkill, CommandType.CastSkill), Is.GreaterThan(0), "скилл не открылся на нужном мастерстве");
        }

        [Test]
        public void PassiveBonusSkill_ChangesCharacteristicsWithoutCasting()
        {
            var baseCore = CreateCore(WithSkill("energy_needed:[energy:{100000}]", EnergyActions));
            var passiveCore = CreateCore(WithSkill("as_bonus_logic:[]", "set_bonus:[bonus_id:{1},target:{0},timing:{0}]"));
            var baseHealth = baseCore.BuildCharacteristics(CreateUnitSnapshot(), BattleSide.Attacking, false, StoryLevelId, 0).MaxHealth;
            var passiveHealth = passiveCore.BuildCharacteristics(CreateUnitSnapshot(), BattleSide.Attacking, false, StoryLevelId, 0).MaxHealth;
            var script = passiveCore.Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(passiveHealth, Is.EqualTo(MathF.Round(baseHealth * 1.15f, MidpointRounding.AwayFromZero)), "бонус пассивного скилла не попал в характеристики");
            Assert.That(FindCommand(script, CommandType.CastSkill), Is.Null, "пассивный скилл кастуется в бою");
        }

        [Test]
        public void PassiveBonusSkill_UsesBonusOfSkillLevel()
        {
            var passiveCore = CreateCore(WithSkill("as_bonus_logic:[]", "set_bonus:[bonus_id:{1:7},target:{0:0},timing:{0:0}]", promoteToSkillLevels: "1;2"));
            var baseSnapshot = CreateUnitSnapshot();
            var promotedSnapshot = CreateUnitSnapshot();

            promotedSnapshot.Level = 2;

            var baseCharacteristics = passiveCore.BuildCharacteristics(baseSnapshot, BattleSide.Attacking, false, StoryLevelId, 0);
            var promotedCharacteristics = passiveCore.BuildCharacteristics(promotedSnapshot, BattleSide.Attacking, false, StoryLevelId, 0);

            Assert.That(promotedCharacteristics.MaxHealth, Is.LessThan(baseCharacteristics.MaxHealth), "на втором уровне должен действовать бонус 7, а не 1");
            Assert.That(promotedCharacteristics.Damage, Is.LessThan(baseCharacteristics.Damage), "бонус 7 снижает урон");
        }

        [Test]
        public void InstantSkill_EnabledOnlyOnLaterLevel_ActivatesOutOfTurn()
        {
            var triggers = "ally_health_lower:[health:{0.95:0.95},is_instant_activation:{0:1}]";
            var actions = "set_bonus:[bonus_id:{1:1},target:{0:0},timing:{0.1:0.1}]";
            var battleCore = CreateCore(WithSkill(triggers, actions, "1;2"));
            var snapshot = CreateUnitSnapshot();

            snapshot.Level = 2;

            var script = battleCore.Replay(CreateReplayData(snapshot));
            var castStepIndex = FindFirstCommandOf(script, CommandType.CastSkill, "actorId", CharacterId);
            var firstHitIndex = FindFirstCommandOf(script, CommandType.ShowDamage, "targetId", CharacterId);

            Assert.That(firstHitIndex, Is.GreaterThanOrEqualTo(0), "персонаж не получил урона, тест ничего не проверяет");
            Assert.That(castStepIndex, Is.GreaterThan(firstHitIndex), "скилл сработал до урона");
            Assert.That(script.Steps[castStepIndex].Turn, Is.EqualTo(script.Steps[firstHitIndex].Turn), "скилл ждал своей очереди вместо внеочередного каста");
        }

        private int FindFirstCommandOf(IBattleScriptResponse script, CommandType commandType, string unitParameter, int unitId)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType == commandType && command.Parameters[unitParameter]!.Value<int>() == unitId)
                        return i;
                }
            }

            return -1;
        }

        [Test]
        public void ReplaceBonus_GrantedTwice_KeepsItsValue()
        {
            var battleCore = CreateCore(_domains);
            var snapshot = CreateUnitSnapshot();

            snapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 8, Count = 2, RemainingBattles = 0 });

            var characteristics = battleCore.BuildCharacteristics(snapshot, BattleSide.Attacking, false, StoryLevelId, 0);

            Assert.That(characteristics.CriticalChance, Is.EqualTo(1f).Within(0.0001f), "replace-бонус умножился на количество выдач");
        }

        [Test]
        public void EquipmentPassiveSkill_UsesFirstLevelRegardlessOfPromote()
        {
            var actions = "set_bonus:[bonus_id:{1:7},target:{0:0},timing:{0:0}]";
            var withSkill = WithEquipmentSkill(actions, "1");
            var withoutSkill = WithEquipmentSkill(actions, string.Empty);
            var snapshot = CreateUnitSnapshot();

            snapshot.Level = 2;
            snapshot.Equipments.Add(new EquipmentSnapshot(1, 1));

            var withCharacteristics = CreateCore(withSkill).BuildCharacteristics(snapshot, BattleSide.Attacking, false, StoryLevelId, 0);
            var withoutCharacteristics = CreateCore(withoutSkill).BuildCharacteristics(snapshot, BattleSide.Attacking, false, StoryLevelId, 0);

            Assert.That(withCharacteristics.MaxHealth, Is.GreaterThan(withoutCharacteristics.MaxHealth), "скилл снаряжения должен дать бонус первого уровня (1)");
            Assert.That(withCharacteristics.Damage, Is.EqualTo(withoutCharacteristics.Damage), "скилл снаряжения взял уровень персонажа (бонус 7)");
        }

        private List<CoreConfigDomain> WithEquipmentSkill(string actions, string equipmentSkillIds)
        {
            var patched = WithSkill("as_bonus_logic:[]", actions, "1;2");

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                ((JObject)rows[0])["skill_ids"] = "";
            });

            return ReplaceDomain(patched, "Equipments", rows =>
            {
                ((JObject)rows[0])["skill_ids"] = equipmentSkillIds;
            });
        }

        [Test]
        public void SummonSpellAmplifiedSkill_UsesSideSpellMultiplier()
        {
            var domains = WithSummonSkill("1");

            domains = ReplaceDomain(domains, "Skills", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if ((string?)row["id"] == "50")
                        row["actions"] = "damage:[damage:{0.5:0.9},target:{1:1},is_spell_amp:{1:1},timing:{0.1:0.1}]";
                }
            });
            domains = ReplaceDomain(domains, "Constants", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if ((string?)row["constant_name"] == "spell_multiplier_base")
                        row["constant_value"] = "1";
                }
            });
            domains = ReplaceDomain(domains, "Bonuses", rows =>
            {
                rows.Add(new JObject
                {
                    ["id"] = "50",
                    ["operator"] = "add",
                    ["bonus_type"] = "spell_multiplier_local",
                    ["bonus_value"] = "1",
                    ["work_modes"] = "permanent",
                });
            });

            var battleCore = CreateCore(domains);
            var summon = CreateSummonSnapshot(1, 1);
            var mainUnit = CreateUnitSnapshot();

            mainUnit.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 50, Count = 1 });

            var summonDamage = battleCore.BuildCharacteristics(summon, BattleSide.Attacking, true, StoryLevelId, 0).Damage;
            var mainSpell = battleCore.BuildCharacteristics(mainUnit, BattleSide.Attacking, false, StoryLevelId, 0).SkillMultiplier;
            var script = battleCore.Replay(CreateReplayData(mainUnit, summon));

            Assert.That(mainSpell, Is.EqualTo(2f));
            Assert.That(FindPhaseDamage(script, BattlePhaseType.SummonSkill, MathF.Round(summonDamage * 0.5f * mainSpell, MidpointRounding.AwayFromZero)), Is.Not.Null, "скилл саммона взял не СПЕЛЛ_МН стороны");
        }

        private List<CoreConfigDomain> WithSummonSkill(string masteryForSkill)
        {
            var patched = ReplaceDomain(_domains, "Skills", rows =>
            {
                rows.Add(new JObject
                {
                    ["id"] = "50",
                    ["triggers"] = "on_cooldown:[cooldown:{1:1},is_start_availiable:{1:1}]",
                    ["actions"] = "damage:[damage:{0.5:0.9},target:{1:1},is_spell_amp:{0:0},timing:{0.1:0.1}]",
                });
            });

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = string.Empty;
            });

            return ReplaceDomain(patched, "Summons", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = "50";
                row["mastery_for_skills"] = masteryForSkill;
                row["skill_upgrade_ids"] = "1";
            });
        }

        private UnitSnapshot CreateSummonSnapshot(int masteryLevel, int skillLevel)
        {
            return new UnitSnapshot
            {
                Id = 1,
                Level = 1,
                MasteryLevel = masteryLevel,
                SlotIndex = 1,
                SkillLevels = new List<int> { skillLevel },
            };
        }

        private BattleReplayData CreateReplayData(UnitSnapshot unitSnapshot, UnitSnapshot summonSnapshot)
        {
            var replayData = CreateReplayData(unitSnapshot);

            replayData.TeamA.Summons.Add(summonSnapshot);

            return replayData;
        }

        private BattleCommand? FindPhaseDamage(IBattleScriptResponse script, BattlePhaseType phase, float expectedDamage)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                if (script.Steps[i].Phase != phase)
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

        private int CountPhaseCommands(IBattleScriptResponse script, BattlePhaseType phase, CommandType commandType)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                if (script.Steps[i].Phase != phase)
                    continue;

                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType == commandType)
                        count += 1;
                }
            }

            return count;
        }

        private List<CoreConfigDomain> WithSkill(string triggers, string actions, string promoteToSkillLevels = "1")
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
