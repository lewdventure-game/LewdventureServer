using Server.Entities;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Infrastructure.Qa
{
    internal sealed class CheatProfileEditor
    {
        private const string ResourceEntry = "resource";
        private const string FlagEntry = "flag";
        private const string CharacterEntry = "character";
        private const string SummonEntry = "summon";
        private const string PromoteEntry = "promote";
        private const string SummonLevelEntry = "summon_level";
        private const string MasteryEntry = "mastery";
        private const string SkillEntry = "skill";
        private const string EquipmentLevelEntry = "equipment_level";
        private const int MaxKeyLength = 64;

        private readonly ProgressionLimits _progressionLimits;
        private readonly RewardApplier _rewardApplier;

        public CheatProfileEditor(ProgressionLimits progressionLimits, RewardApplier rewardApplier)
        {
            _progressionLimits = progressionLimits;
            _rewardApplier = rewardApplier;
        }

        public bool SetResource(PlayerProfileDocument profile, string key, long amount, List<PlayerLedgerEntryDocument> entries, out string error)
        {
            if (IsValidKey(key) == false)
            {
                error = $"Resource key {key} is not valid: use a-z, 0-9 and _.";

                return false;
            }

            if (amount < 0)
            {
                error = "Resource amount cannot be negative.";

                return false;
            }

            profile.Resources.TryGetValue(key, out var current);
            profile.Resources[key] = amount;

            if (current != amount)
                entries.Add(CreateEntry(ResourceEntry, key, amount - current));

            error = string.Empty;

            return true;
        }

        public bool SetFlag(PlayerProfileDocument profile, string key, int value, List<PlayerLedgerEntryDocument> entries, out string error)
        {
            if (IsValidKey(key) == false)
            {
                error = $"Flag key {key} is not valid: use a-z, 0-9 and _.";

                return false;
            }

            if (value < 0)
            {
                error = "Flag value cannot be negative.";

                return false;
            }

            profile.Flags.TryGetValue(key, out var current);

            if (value == 0)
                profile.Flags.Remove(key);
            else
                profile.Flags[key] = value;

            if (current != value)
                entries.Add(CreateEntry(FlagEntry, key, value - current));

            error = string.Empty;

            return true;
        }

        public bool SetCharacterPromote(
            PlayerProfileDocument profile,
            int characterId,
            int promoteLevel,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries,
            out string error)
        {
            if (configDistributor.Characters.TryGet(characterId, out var mapper) == false)
            {
                error = $"Character {characterId} is missing in configs.";

                return false;
            }

            var maxLevel = _progressionLimits.GetMaxPromoteLevel(mapper, configDistributor);

            if (promoteLevel < 1 || maxLevel < promoteLevel)
            {
                error = $"Character {characterId} promote level must be from 1 to {maxLevel}.";

                return false;
            }

            var character = FindCharacter(profile, characterId);

            if (character == null)
            {
                character = new PlayerCharacterDocument { ConfigId = characterId, UnlockedAt = now };
                profile.Characters.Add(character);
                entries.Add(CreateEntry(CharacterEntry, characterId.ToString(), 1));
            }

            var previousLevel = character.PromoteLevel;

            character.PromoteLevel = promoteLevel;

            if (previousLevel != promoteLevel)
                entries.Add(CreateEntry(PromoteEntry, $"{characterId}:{promoteLevel}", promoteLevel - previousLevel));

            for (int level = previousLevel + 1; level <= promoteLevel; level++)
                entries.AddRange(_rewardApplier.ApplyCharacterPromoteRewards(profile, mapper, level, configDistributor, now));

            error = string.Empty;

            return true;
        }

        public bool SetSummon(
            PlayerProfileDocument profile,
            int summonId,
            int? level,
            int? masteryLevel,
            int? skillLevel,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries,
            out string error)
        {
            if (configDistributor.Summons.TryGet(summonId, out var mapper) == false)
            {
                error = $"Summon {summonId} is missing in configs.";

                return false;
            }

            var maxLevel = _progressionLimits.GetMaxSummonLevel(mapper, configDistributor);
            var maxMastery = _progressionLimits.GetMaxMasteryLevel(mapper, configDistributor);

            if (level != null && (level.Value < 1 || maxLevel < level.Value))
            {
                error = $"Summon {summonId} level must be from 1 to {maxLevel}.";

                return false;
            }

            if (masteryLevel != null && (masteryLevel.Value < 1 || maxMastery < masteryLevel.Value))
            {
                error = $"Summon {summonId} mastery must be from 1 to {maxMastery}.";

                return false;
            }

            if (skillLevel != null && skillLevel.Value < 1)
            {
                error = "Skill level must be at least 1.";

                return false;
            }

            var summon = FindSummon(profile, summonId);

            if (summon == null)
            {
                summon = new PlayerSummonDocument { ConfigId = summonId, Level = 1, MasteryLevel = 1, UnlockedAt = now };
                profile.Summons.Add(summon);
                entries.Add(CreateEntry(SummonEntry, summonId.ToString(), 1));
                entries.AddRange(_rewardApplier.ApplySummonMasteryRewards(profile, summon, mapper, configDistributor, now));
            }

            if (level != null && summon.Level != level.Value)
            {
                entries.Add(CreateEntry(SummonLevelEntry, $"{summonId}:{level.Value}", level.Value - summon.Level));
                summon.Level = level.Value;
            }

            if (masteryLevel != null)
                ApplyMastery(profile, summon, mapper, masteryLevel.Value, configDistributor, now, entries);

            if (skillLevel != null)
                ApplySkillLevels(summon, mapper, skillLevel.Value, configDistributor, entries);

            error = string.Empty;

            return true;
        }

        public bool SetEquipmentLevel(
            PlayerProfileDocument profile,
            string instanceId,
            int level,
            IConfigDistributor configDistributor,
            List<PlayerLedgerEntryDocument> entries,
            out string error)
        {
            var equipment = FindEquipment(profile, instanceId);

            if (equipment == null)
            {
                error = $"Equipment instance {instanceId} is not owned by the player.";

                return false;
            }

            if (configDistributor.Equipments.TryGet(equipment.ConfigId, out var mapper) == false)
            {
                error = $"Equipment {equipment.ConfigId} is missing in configs.";

                return false;
            }

            var maxLevel = _progressionLimits.GetMaxEquipmentLevel(mapper, configDistributor);

            if (level < 1 || maxLevel < level)
            {
                error = $"Equipment {equipment.ConfigId} level must be from 1 to {maxLevel}.";

                return false;
            }

            if (equipment.Level != level)
            {
                entries.Add(CreateEntry(EquipmentLevelEntry, $"{instanceId}:{level}", level - equipment.Level));
                equipment.Level = level;
            }

            error = string.Empty;

            return true;
        }

        public void MaxOut(PlayerProfileDocument profile, IConfigDistributor configDistributor, DateTime now, List<PlayerLedgerEntryDocument> entries)
        {
            for (int i = 0; i < profile.Characters.Count; i++)
            {
                var characterId = profile.Characters[i].ConfigId;

                if (configDistributor.Characters.TryGet(characterId, out var characterMapper) == false)
                    continue;

                var maxPromote = _progressionLimits.GetMaxPromoteLevel(characterMapper, configDistributor);

                SetCharacterPromote(profile, characterId, maxPromote, configDistributor, now, entries, out _);
            }

            for (int i = 0; i < profile.Summons.Count; i++)
            {
                var summonId = profile.Summons[i].ConfigId;

                if (configDistributor.Summons.TryGet(summonId, out var summonMapper) == false)
                    continue;

                var maxLevel = _progressionLimits.GetMaxSummonLevel(summonMapper, configDistributor);
                var maxMastery = _progressionLimits.GetMaxMasteryLevel(summonMapper, configDistributor);

                SetSummon(profile, summonId, maxLevel, maxMastery, int.MaxValue, configDistributor, now, entries, out _);
            }

            for (int i = 0; i < profile.Equipment.Count; i++)
            {
                var equipment = profile.Equipment[i];

                if (configDistributor.Equipments.TryGet(equipment.ConfigId, out var equipmentMapper) == false)
                    continue;

                var maxLevel = _progressionLimits.GetMaxEquipmentLevel(equipmentMapper, configDistributor);

                SetEquipmentLevel(profile, equipment.InstanceId, maxLevel, configDistributor, entries, out _);
            }
        }

        private void ApplyMastery(
            PlayerProfileDocument profile,
            PlayerSummonDocument summon,
            ISummonMapper mapper,
            int masteryLevel,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            var previousLevel = summon.MasteryLevel;

            if (previousLevel == masteryLevel)
                return;

            entries.Add(CreateEntry(MasteryEntry, $"{mapper.Id}:{masteryLevel}", masteryLevel - previousLevel));

            if (masteryLevel < previousLevel)
            {
                summon.MasteryLevel = masteryLevel;

                return;
            }

            for (int level = previousLevel + 1; level <= masteryLevel; level++)
            {
                summon.MasteryLevel = level;
                entries.AddRange(_rewardApplier.ApplySummonMasteryRewards(profile, summon, mapper, configDistributor, now));
            }
        }

        private void ApplySkillLevels(
            PlayerSummonDocument summon,
            ISummonMapper mapper,
            int skillLevel,
            IConfigDistributor configDistributor,
            List<PlayerLedgerEntryDocument> entries)
        {
            for (int i = 0; i < mapper.SkillIds.Length; i++)
            {
                var maxLevel = _progressionLimits.GetMaxSkillLevel(mapper, i, configDistributor);
                var target = maxLevel < skillLevel ? maxLevel : skillLevel;

                while (summon.SkillLevels.Count <= i)
                    summon.SkillLevels.Add(1);

                var current = summon.SkillLevels[i] < 1 ? 1 : summon.SkillLevels[i];

                if (current == target)
                    continue;

                summon.SkillLevels[i] = target;
                entries.Add(CreateEntry(SkillEntry, $"{mapper.Id}:{mapper.SkillIds[i].Trim()}:{target}", target - current));
            }
        }

        private bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || MaxKeyLength < key.Length)
                return false;

            for (int i = 0; i < key.Length; i++)
            {
                var symbol = key[i];

                if (char.IsAsciiLetterLower(symbol) == false && char.IsAsciiDigit(symbol) == false && symbol != '_')
                    return false;
            }

            return true;
        }

        private PlayerCharacterDocument? FindCharacter(PlayerProfileDocument profile, int characterId)
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

        private PlayerLedgerEntryDocument CreateEntry(string type, string key, long amount)
        {
            return new PlayerLedgerEntryDocument { Type = type, Key = key, Amount = amount };
        }
    }
}
