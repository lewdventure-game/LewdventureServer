using System.Globalization;
using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Runs;
using Server.Services;
using Server.Stories;

namespace Server.Balance
{
    internal sealed class BalanceSnapshotFactory
    {
        private const string BalanceUserId = "balance";
        private const string EquipmentInstancePrefix = "balance_eq_";
        private const string EquipmentSlotPrefix = "slot";

        private readonly RunSnapshotBuilder _runSnapshotBuilder;

        public BalanceSnapshotFactory(RunSnapshotBuilder runSnapshotBuilder)
        {
            _runSnapshotBuilder = runSnapshotBuilder;
        }

        public PlayerProfileDocument CreateProfile(BalanceSetup setup)
        {
            var profile = new PlayerProfileDocument { Id = BalanceUserId };

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = setup.CharacterId, PromoteLevel = Math.Max(1, setup.PromoteLevel) });
            profile.Loadout.CharacterId = setup.CharacterId;

            for (int i = 0; i < setup.Summons.Count; i++)
            {
                var summon = setup.Summons[i];

                profile.Summons.Add(new PlayerSummonDocument
                {
                    ConfigId = summon.Id,
                    Level = Math.Max(1, summon.Level),
                    MasteryLevel = Math.Max(1, summon.MasteryLevel),
                    SkillLevels = new List<int>(summon.SkillLevels),
                });
                profile.Loadout.Summons.Add(summon.Id);
            }

            for (int i = 0; i < setup.Equipment.Count; i++)
            {
                var equipment = setup.Equipment[i];
                var instanceId = EquipmentInstancePrefix + i.ToString(CultureInfo.InvariantCulture);

                profile.Equipment.Add(new PlayerEquipmentDocument { InstanceId = instanceId, ConfigId = equipment.ConfigId, Level = Math.Max(1, equipment.Level) });
                profile.Loadout.Equipment[EquipmentSlotPrefix + i.ToString(CultureInfo.InvariantCulture)] = instanceId;
            }

            for (int i = 0; i < setup.AccountBonuses.Count; i++)
            {
                var bonus = setup.AccountBonuses[i];

                profile.Bonuses.Add(new PlayerBonusDocument { BonusId = bonus.Id, Count = Math.Max(1, bonus.Count) });
            }

            return profile;
        }

        public RunDocument CreateRun(BalanceSetup setup, BalanceTarget target)
        {
            var run = new RunDocument
            {
                Id = BalanceUserId,
                UserId = BalanceUserId,
                StoryLevelId = target.StoryLevelId,
                CurrentHealth = setup.CurrentHealth,
            };

            run.Perks.AddRange(setup.RunPerks);
            run.Statuses.AddRange(setup.RunStatuses);

            for (int i = 0; i < setup.RunBonuses.Count; i++)
            {
                var bonus = setup.RunBonuses[i];

                run.Bonuses.Add(new RunBonusDocument { BonusId = bonus.Id, Count = Math.Max(1, bonus.Count), RemainingBattles = bonus.RemainingBattles });
            }

            return run;
        }

        public bool TryBuild(BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor, IBattleParameterParser battleParameterParser, out BattleSimulationData simulationData, out string error)
        {
            simulationData = null!;

            if (TryResolveEnemies(target, configDistributor, battleParameterParser, out var enemyIds, out error) == false)
                return false;

            var profile = CreateProfile(setup);
            var run = CreateRun(setup, target);
            var stage = new RunStageDocument { StageId = target.StageId, EventId = target.EventId };

            return _runSnapshotBuilder.TryBuild(profile, run, stage, enemyIds, configDistributor, out simulationData, out error);
        }

        public UnitSnapshot BuildMainUnit(BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor)
        {
            var profile = CreateProfile(setup);
            var run = CreateRun(setup, target);

            return _runSnapshotBuilder.BuildMainUnit(profile, run, profile.Characters[0], configDistributor);
        }

        public List<int> CollectSummonIds(BalanceSetup setup)
        {
            return _runSnapshotBuilder.CollectSummonIds(CreateProfile(setup));
        }

        private bool TryResolveEnemies(BalanceTarget target, IConfigDistributor configDistributor, IBattleParameterParser battleParameterParser, out List<int> enemyIds, out string error)
        {
            error = string.Empty;
            enemyIds = new List<int>(target.EnemyIds);

            if (0 < enemyIds.Count)
                return true;

            if (configDistributor.StoryEvents.TryGet(target.EventId, out var storyEvent) == false)
            {
                error = $"Story event {target.EventId} is missing in configs; choose a fight event or list enemies.";

                return false;
            }

            if (storyEvent.EventType != StoryEventType.Fight)
            {
                error = $"Story event {target.EventId} is {storyEvent.EventType}, not a fight.";

                return false;
            }

            var parameters = new Dictionary<string, string>();

            battleParameterParser.ParseKeyValues(storyEvent.EventParameters, parameters);

            if (parameters.TryGetValue(RunEventKeys.Enemies, out var raw))
                enemyIds = ParseIds(raw);

            if (enemyIds.Count == 0)
                error = $"Fight event {target.EventId} has no enemies.";

            return enemyIds.Count != 0;
        }

        private List<int> ParseIds(string raw)
        {
            var result = new List<int>();
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    result.Add(value);
            }

            return result;
        }
    }
}
