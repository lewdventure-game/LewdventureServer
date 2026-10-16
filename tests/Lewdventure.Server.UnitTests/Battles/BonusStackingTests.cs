using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Logging;
using Server.Shared;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class BonusStackingTests
    {
        private const int CharacterId = 1;
        private const int EnemyId = 10101;
        private const int StrongBurningStatusId = 2;
        private const int StrongBurningBonusId = 7;
        private const float StrongBurningBonusValue = -0.05f;
        private const int ActionRewardPerkId = 6;
        private const int ActionRewardBonusId = 1;
        private const float ActionRewardBonusValue = 0.15f;
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
        public void StrongStatus_ReappliedAfterExpiry_KeepsOneBonusPerStack()
        {
            var patched = WithCharacterSkill(_domains, "on_cooldown:[cooldown:{1},is_start_availiable:{1}]", "set_status:[status_id:{2},target:{1},timing:{0.1}]");

            patched = WithDurableEnemy(patched);

            var script = CreateCore(patched).Replay(CreateReplayData(CreateUnitSnapshot()));
            var stacks = 0;
            var expiredOnce = false;
            var reappliedAfterExpiry = false;
            var checkedSteps = 0;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;
                var bonusValue = float.NaN;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType == CommandType.RemoveStatus && IsStrongBurningOnEnemy(command))
                    {
                        stacks = 0;
                        expiredOnce = true;
                    }

                    if (command.CommandType == CommandType.ApplyStatus && IsStrongBurningOnEnemy(command))
                    {
                        stacks = command.Parameters["stacks"]!.Value<int>();

                        if (expiredOnce && 2 <= stacks)
                            reappliedAfterExpiry = true;
                    }

                    if (command.CommandType == CommandType.SetBonus && IsBonusOnUnit(command, EnemyId, StrongBurningBonusId))
                        bonusValue = command.Parameters["value"]!.Value<float>();
                }

                if (float.IsNaN(bonusValue) || stacks == 0)
                    continue;

                Assert.That(bonusValue, Is.EqualTo(StrongBurningBonusValue * stacks).Within(0.0001f), $"шаг {i}: бонус не совпал с количеством стаков {stacks}");

                checkedSteps += 1;
            }

            Assert.That(reappliedAfterExpiry, Is.True, "сценарий не дошёл до повторного наложения после истечения стака");
            Assert.That(0 < checkedSteps, Is.True);
        }

        [Test]
        public void ActionRewardPerk_RepeatedTriggers_StackBonus()
        {
            var patched = WithoutCharacterSkills(_domains);

            patched = WithDurableEnemy(patched);

            var unitSnapshot = CreateUnitSnapshot();

            unitSnapshot.ActivePerkIds.Add(ActionRewardPerkId);

            var script = CreateCore(patched).Replay(CreateReplayData(unitSnapshot));
            var maxValue = 0f;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.SetBonus || IsBonusOnUnit(command, CharacterId, ActionRewardBonusId) == false)
                        continue;

                    maxValue = MathF.Max(maxValue, command.Parameters["value"]!.Value<float>());
                }
            }

            Assert.That(ActionRewardBonusValue * 2f - 0.0001f <= maxValue, Is.True, $"бонус перка не сложился, максимум = {maxValue}");
        }

        [Test]
        public void SkillSetBonus_RecastStacksBonus()
        {
            var patched = WithCharacterSkill(_domains, "on_cooldown:[cooldown:{1},is_start_availiable:{1}]", "set_bonus:[bonus_id:{1},target:{0},timing:{0.1}]");

            patched = WithDurableEnemy(patched);

            var script = CreateCore(patched).Replay(CreateReplayData(CreateUnitSnapshot()));
            var setBonusCount = 0;
            var maxValue = 0f;

            for (int i = 0; i < script.Steps.Count; i++)
            {
                var commands = script.Steps[i].Commands;

                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var command = commands[commandIndex];

                    if (command.CommandType != CommandType.SetBonus || IsBonusOnUnit(command, CharacterId, ActionRewardBonusId) == false)
                        continue;

                    setBonusCount += 1;
                    maxValue = MathF.Max(maxValue, command.Parameters["value"]!.Value<float>());
                }
            }

            Assert.That(1 < setBonusCount, Is.True, "скилл не кастовался повторно");
            Assert.That(ActionRewardBonusValue * 2f - 0.0001f <= maxValue, Is.True, $"бонус скилла не сложился, максимум = {maxValue}");

            var reportedStacks = 0;

            for (int i = 0; i < script.BonusGrants.Count; i++)
            {
                if (script.BonusGrants[i].Id == ActionRewardBonusId)
                    reportedStacks += script.BonusGrants[i].Count;
            }

            Assert.That(reportedStacks, Is.EqualTo(setBonusCount), "выдачи set_bonus с end_of_game/permanent не ушли в забег");
        }

        private bool IsStrongBurningOnEnemy(BattleCommand command)
        {
            return command.Parameters["targetId"]!.Value<int>() == EnemyId
                && command.Parameters["statusId"]!.Value<int>() == StrongBurningStatusId;
        }

        private bool IsBonusOnUnit(BattleCommand command, int unitId, int bonusId)
        {
            return command.Parameters["unitId"]!.Value<int>() == unitId
                && command.Parameters["bonusId"]!.Value<int>() == bonusId;
        }

        private List<CoreConfigDomain> WithCharacterSkill(IReadOnlyList<CoreConfigDomain> domains, string triggers, string actions)
        {
            var patched = ReplaceDomain(domains, "Skills", rows =>
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
                row["promote_to_skill_levels"] = "1";
            });
        }

        private List<CoreConfigDomain> WithoutCharacterSkills(IReadOnlyList<CoreConfigDomain> domains)
        {
            return ReplaceDomain(domains, "Characters", rows =>
            {
                var row = (JObject)rows[0];

                row["skill_ids"] = "";
            });
        }

        private List<CoreConfigDomain> WithDurableEnemy(IReadOnlyList<CoreConfigDomain> domains)
        {
            return ReplaceDomain(domains, "Enemies", rows =>
            {
                var row = (JObject)rows[0];

                row["health"] = "1000000";
                row["damage"] = "1";
                row["skill_ids"] = "";
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
    }
}
