using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Services;

namespace Server.Runs
{
    internal sealed class RunSnapshotBuilder
    {
        public bool TryBuild(
            PlayerProfileDocument profile,
            RunDocument run,
            RunStageDocument stage,
            IReadOnlyList<int> enemyIds,
            IConfigDistributor configDistributor,
            out BattleSimulationData simulationData,
            out string error)
        {
            simulationData = null!;
            error = string.Empty;

            var characterId = profile.Loadout.CharacterId;

            if (characterId <= 0)
            {
                error = "Loadout has no character.";

                return false;
            }

            var character = FindCharacter(profile, characterId);

            if (character == null)
            {
                error = $"Character {characterId} is not unlocked.";

                return false;
            }

            if (configDistributor.Characters.TryGet(characterId, out _) == false)
            {
                error = $"Character {characterId} is missing in configs.";

                return false;
            }

            if (enemyIds.Count == 0)
            {
                error = "Fight event has no enemies.";

                return false;
            }

            var teamA = new TeamSnapshot();
            var teamB = new TeamSnapshot();

            teamA.MainUnits.Add(BuildMainUnit(profile, run, character));
            AddSummons(profile, teamA);

            for (int i = 0; i < enemyIds.Count; i++)
            {
                if (configDistributor.Enemies.TryGet(enemyIds[i], out _) == false)
                {
                    error = $"Enemy {enemyIds[i]} is missing in configs.";

                    return false;
                }

                teamB.MainUnits.Add(new UnitSnapshot
                {
                    Id = enemyIds[i],
                    Level = 1,
                    SlotIndex = i,
                });
            }

            simulationData = new BattleSimulationData
            {
                TeamA = teamA,
                TeamB = teamB,
                StoryLevelId = run.StoryLevelId,
                StageId = stage.StageId,
            };

            return true;
        }

        public UnitSnapshot BuildMainUnit(PlayerProfileDocument profile, RunDocument run, PlayerCharacterDocument character)
        {
            var unit = new UnitSnapshot
            {
                Id = character.ConfigId,
                Level = character.UpgradesApplied + 1,
                SlotIndex = 0,
                CurrentHealth = run.CurrentHealth,
            };

            foreach (var pair in profile.Loadout.Equipment)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    continue;

                var instance = FindEquipment(profile, pair.Value);

                if (instance == null)
                    continue;

                unit.Equipments.Add(new EquipmentSnapshot(instance.ConfigId, instance.Level));
                unit.EquipmentIds.Add(instance.ConfigId);
            }

            for (int i = 0; i < run.Perks.Count; i++)
                unit.ActivePerkIds.Add(run.Perks[i]);

            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                var bonus = run.Bonuses[i];

                unit.ActiveBonuses.Add(new BonusGrantSnapshot
                {
                    Id = bonus.BonusId,
                    Count = bonus.Count,
                    RemainingBattles = bonus.RemainingBattles,
                });
            }

            return unit;
        }

        public void AddSummons(PlayerProfileDocument profile, TeamSnapshot team)
        {
            var summonIds = profile.Loadout.Summons;

            for (int i = 0; i < summonIds.Count; i++)
            {
                var summon = FindSummon(profile, summonIds[i]);

                if (summon == null)
                    continue;

                team.Summons.Add(new UnitSnapshot
                {
                    Id = summon.ConfigId,
                    Level = summon.Level,
                    MasteryLevel = summon.MasteryLevel,
                    SlotIndex = i,
                });
            }
        }

        public PlayerCharacterDocument? FindCharacter(PlayerProfileDocument profile, int characterId)
        {
            for (int i = 0; i < profile.Characters.Count; i++)
            {
                if (profile.Characters[i].ConfigId == characterId)
                    return profile.Characters[i];
            }

            return null;
        }

        private PlayerSummonDocument? FindSummon(PlayerProfileDocument profile, int summonId)
        {
            for (int i = 0; i < profile.Summons.Count; i++)
            {
                if (profile.Summons[i].ConfigId == summonId)
                    return profile.Summons[i];
            }

            return null;
        }

        private PlayerEquipmentDocument? FindEquipment(PlayerProfileDocument profile, string instanceId)
        {
            for (int i = 0; i < profile.Equipment.Count; i++)
            {
                if (string.Equals(profile.Equipment[i].InstanceId, instanceId, StringComparison.Ordinal))
                    return profile.Equipment[i];
            }

            return null;
        }
    }
}
