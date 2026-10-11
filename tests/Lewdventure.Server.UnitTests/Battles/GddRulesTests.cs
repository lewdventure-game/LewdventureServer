using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Logging;
using Server.Shared;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class GddRulesTests
    {
        private const int CharacterId = 1;
        private const int EnemyId = 10101;
        private const int BurningStatusId = 1;
        private const int ResurrectionPerkId = 7;
        private const int FirePerkId = 2;
        private const int PoisonStatusId = 3;
        private const int SecondEnemyId = 10102;
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
        public void CharacterWithoutBonusColumns_UsesConstantsOnly()
        {
            var battleCore = CreateCore(_domains);
            var characteristics = battleCore.BuildCharacteristics(CreateUnitSnapshot(), BattleSide.Attacking, false, 0, 0);

            Assert.That(characteristics.MaxHealth, Is.EqualTo(600f).Within(0.001f));
        }

        [Test]
        public void AccountBonus_FromSnapshot_ChangesCharacteristics()
        {
            var battleCore = CreateCore(_domains);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 1, Count = 1, RemainingBattles = 0 });

            var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, 0, 0);

            Assert.That(characteristics.MaxHealth, Is.EqualTo(690f).Within(0.001f));
        }

        [Test]
        public void Vampyrism_HealsOnAttacksOnly()
        {
            var patched = ReplaceDomain(_domains, "Bonuses", rows =>
            {
                var row = (JObject)rows[0];

                row["bonus_type"] = "vampyrism_local";
                row["bonus_value"] = "0.5";
                row["work_mode"] = "permanent";
            });

            patched = ReplaceDomain(patched, "Skills", rows =>
            {
                rows.Clear();
                rows.Add(new JObject
                {
                    ["id"] = "1",
                    ["triggers"] = "on_cooldown:[cooldown:{1},is_start_availiable:{1}]",
                    ["actions"] = "damage:[damage:{1},target:{1},is_spell_amp:{0},timing:{0.1}]",
                });
            });

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = "1";
                row["promote_to_skill_levels"] = "0";
            });

            var battleCore = CreateCore(patched);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 1, Count = 1, RemainingBattles = 0 });

            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var healedInAttack = false;
            var healedInSkill = false;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var step = script.Steps[i];
                var commands = step.Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType != CommandType.ShowHeal)
                        continue;

                    if (step.Phase == BattlePhaseType.UnitSkill || step.Phase == BattlePhaseType.EnergySkill)
                        healedInSkill = true;
                    else
                        healedInAttack = true;
                }
            }

            Assert.That(healedInAttack, Is.True, "вампиризм не сработал на обычной атаке");
            Assert.That(healedInSkill, Is.False, "вампиризм сработал на скилле");
        }

        [Test]
        public void DroppedEquipmentSpellMultiplierBonus_IsIgnoredWithoutBreakingBattle()
        {
            var patched = ReplaceDomain(_domains, "Bonuses", rows =>
            {
                var row = (JObject)rows[0];

                row["bonus_type"] = "equip_spell_multiplier_local";
                row["bonus_value"] = "0.5";
            });

            var battleCore = CreateCore(patched);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActiveBonuses.Add(new BonusGrantSnapshot { Id = 1, Count = 1, RemainingBattles = 0 });

            var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, 0, 0);

            Assert.That(characteristics.MaxHealth, Is.EqualTo(600f).Within(0.001f));
            Assert.That(characteristics.SkillMultiplier, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void StatusWithoutSource_UsesFlatValueReducedByDefence()
        {
            var patched = ReplaceDomain(_domains, "Statuses", rows =>
            {
                var row = (JObject)rows[0];

                row["parameters"] = "damage_ratio:[0.1];\nflat_value:[100];\ndamage_length:[3];\nmax_stacks:[5]";
            });

            var battleCore = CreateCore(patched);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActiveStatusIds.Add(BurningStatusId);

            var characteristics = battleCore.BuildCharacteristics(unitSnapshot, BattleSide.Attacking, false, 0, 0);
            var expected = MathF.Round(100f * (1f - characteristics.Defence), MidpointRounding.AwayFromZero);
            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var tick = FindFirstStatusTick(script);

            Assert.That(tick, Is.Not.Null);
            Assert.That(ReadTickValue(tick!), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void StatusWithoutSourceAndFlatValue_DealsNoDamage()
        {
            var battleCore = CreateCore(_domains);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActiveStatusIds.Add(BurningStatusId);

            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var tick = FindFirstStatusTick(script);

            Assert.That(tick, Is.Not.Null);
            Assert.That(ReadTickValue(tick!), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ResurrectionPerk_ReportsUsageAfterBattle()
        {
            var battleCore = CreateCore(_domains);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActivePerkIds.Add(ResurrectionPerkId);

            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var usage = FindPerkUsage(script, ResurrectionPerkId);

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage!.UsedCount, Is.EqualTo(1));
            Assert.That(usage.RemainingUses, Is.EqualTo(0));
        }

        [Test]
        public void ResurrectionPerk_WithSpentUsage_DoesNotTriggerAgain()
        {
            var battleCore = CreateCore(_domains);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActivePerkIds.Add(ResurrectionPerkId);
            unitSnapshot.PerkUsages.Add(new PerkUsage { PerkId = ResurrectionPerkId, UsedCount = 1 });

            var script = battleCore.Replay(CreateReplayData(unitSnapshot));
            var usage = FindPerkUsage(script, ResurrectionPerkId);

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage!.UsedCount, Is.EqualTo(1));
            Assert.That(usage.RemainingUses, Is.EqualTo(0));
            Assert.That(CountPerkTriggers(script, ResurrectionPerkId), Is.EqualTo(0));
        }

        [Test]
        public void TurnLimit_KillsPlayerUnit()
        {
            var patched = ReplaceDomain(_domains, "Story_levels", rows =>
            {
                var row = (JObject)rows[0];

                row["max_battle_turns"] = "1";
            });

            patched = ReplaceDomain(patched, "Enemies", rows =>
            {
                var row = (JObject)rows[0];

                row["health"] = "1000000";
                row["damage"] = "1";
            });

            var battleCore = CreateCore(patched);
            var script = battleCore.Replay(CreateReplayData(CreateUnitSnapshot()));

            Assert.That(script.OutcomeType, Is.EqualTo(OutcomeType.Timeout));
            Assert.That(CountKillUnit(script, CharacterId), Is.EqualTo(1));
        }

        [Test]
        public void FirePerk_OnPoisonedEnemy_UsesDebuffChanceInsteadOfProduct()
        {
            var patched = ReplaceDomain(_domains, "Perks", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    if (row["id"]!.ToString() != FirePerkId.ToString())
                        continue;

                    row["perk_parameters"] = "projectile_count:[3];\ndamage_ratio:[0.15];\nhit_rewards:[status:1:1];\nrewards_chance:[0];\ndebuff_rewards_chance:[1];\nproc_rounds:[1,2,3]";
                }
            });

            var battleCore = CreateCore(patched);
            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActivePerkIds.Add(FirePerkId);

            var replayData = CreateReplayData(unitSnapshot);

            replayData.TeamB.MainUnits[0].ActiveStatusIds.Add(PoisonStatusId);

            var script = battleCore.Replay(replayData);

            Assert.That(CountAppliedStatus(script, EnemyId, BurningStatusId), Is.GreaterThan(0), "при яде на враге шанс обязан быть debuff_rewards_chance, а не rewards_chance * debuff_rewards_chance");
        }

        [Test]
        public void SkillDamageOnAllEnemies_KillsUnitOutsideMainTarget()
        {
            var patched = ReplaceDomain(_domains, "Skills", rows =>
            {
                rows.Clear();
                rows.Add(new JObject
                {
                    ["id"] = "1",
                    ["triggers"] = "on_cooldown:[cooldown:{1},is_start_availiable:{1}]",
                    ["actions"] = "damage:[damage:{1},target:{3},is_spell_amp:{0},timing:{0.1}]",
                });
            });

            patched = ReplaceDomain(patched, "Characters", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = "1";
                row["promote_to_skill_levels"] = "1";
            });

            patched = ReplaceDomain(patched, "Enemies", rows =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = (JObject)rows[i];

                    row["skill_ids"] = "";
                    row["damage"] = "1";
                    row["health"] = row["id"]!.ToString() == EnemyId.ToString() ? "1000000" : "1";
                }
            });

            var replayData = CreateReplayData(CreateUnitSnapshot());

            replayData.TeamB.MainUnits.Add(new UnitSnapshot
            {
                Id = SecondEnemyId,
                Level = 1,
                SlotIndex = 1,
            });

            var script = CreateCore(patched).Replay(replayData);

            Assert.That(CountKillUnit(script, SecondEnemyId), Is.EqualTo(1), "смерть от скилла по второму слоту не попала в сценарий");
        }

        private int CountAppliedStatus(IBattleScriptResponse script, int targetId, int statusId)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.ApplyStatus)
                        continue;

                    if (command.Parameters["targetId"]!.Value<int>() == targetId && command.Parameters["statusId"]!.Value<int>() == statusId)
                        count += 1;
                }
            }

            return count;
        }

        private int CountKillUnit(IBattleScriptResponse script, int unitId)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.KillUnit)
                        continue;

                    if (command.Parameters["unitId"]!.Value<int>() == unitId)
                        count += 1;
                }
            }

            return count;
        }

        private PerkUsage? FindPerkUsage(IBattleScriptResponse script, int perkId)
        {
            var usages = script.PerkUsages;

            for (int i = 0; i < usages.Count; i++)
            {
                if (usages[i].PerkId == perkId)
                    return usages[i];
            }

            return null;
        }

        private int CountPerkTriggers(IBattleScriptResponse script, int perkId)
        {
            var count = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.TriggerPerk)
                        continue;

                    if (command.Parameters["perkId"]!.Value<int>() == perkId)
                        count += 1;
                }
            }

            return count;
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

        private float ReadTickValue(BattleCommand command)
        {
            return command.Parameters["value"]!.Value<float>();
        }

        private BattleCommand? FindFirstStatusTick(IBattleScriptResponse script)
        {
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (commands[commandIndex].CommandType == CommandType.TickStatus)
                        return commands[commandIndex];
                }
            }

            return null;
        }
    }
}
