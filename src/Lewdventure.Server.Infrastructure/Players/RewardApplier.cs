using Server.Battles;
using Server.Bonuses;
using Server.Configs;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class RewardApplier
    {
        private const string CharacterOverflowConstant = "character_overflow_resource";
        private const string EquipmentInstancePrefix = "eq_";
        private const string ResourceEntry = "resource";
        private const string CharacterEntry = "character";
        private const string SummonEntry = "summon";
        private const string EquipmentEntry = "equipment";
        private const string FlagEntry = "flag";
        private const string BonusEntry = "bonus";

        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly ILogger<RewardApplier> _logger;

        public RewardApplier(
            IBattleRewardParser battleRewardParser,
            IBonusWorkModeParser bonusWorkModeParser,
            ILogger<RewardApplier> logger)
        {
            _battleRewardParser = battleRewardParser;
            _bonusWorkModeParser = bonusWorkModeParser;
            _logger = logger;
        }

        public List<PlayerLedgerEntryDocument> Apply(
            PlayerProfileDocument profile,
            string rawRewards,
            IConfigDistributor configDistributor,
            DateTime now)
        {
            if (string.IsNullOrWhiteSpace(rawRewards))
                return new List<PlayerLedgerEntryDocument>();

            return Apply(profile, _battleRewardParser.Parse(rawRewards), configDistributor, now);
        }

        public List<PlayerLedgerEntryDocument> Apply(
            PlayerProfileDocument profile,
            IReadOnlyList<BattleReward> rewards,
            IConfigDistributor configDistributor,
            DateTime now)
        {
            var entries = new List<PlayerLedgerEntryDocument>();

            for (int i = 0; i < rewards.Count; i++)
                ApplyReward(profile, rewards[i], configDistributor, now, entries);

            return entries;
        }

        private void ApplyReward(
            PlayerProfileDocument profile,
            in BattleReward reward,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (reward.Type == BattleRewardType.Resource)
            {
                if (reward.HasStringRewardKey == false)
                {
                    _logger.LogWarning("[Player] resource reward without key skipped count = {Count}", reward.Count);

                    return;
                }

                AddResource(profile, reward.RewardKey, reward.Count, entries);

                return;
            }

            if (reward.Type == BattleRewardType.Account)
            {
                if (reward.HasStringRewardKey == false)
                {
                    _logger.LogWarning("[Player] account reward without key skipped count = {Count}", reward.Count);

                    return;
                }

                profile.Flags.TryGetValue(reward.RewardKey, out var current);
                profile.Flags[reward.RewardKey] = current + reward.Count;
                entries.Add(CreateEntry(FlagEntry, reward.RewardKey, reward.Count));

                return;
            }

            if (reward.Type == BattleRewardType.Character)
            {
                AddCharacter(profile, reward.Id, reward.Count, configDistributor, now, entries);

                return;
            }

            if (reward.Type == BattleRewardType.Summon)
            {
                AddSummon(profile, reward.Id, reward.Count, configDistributor, now, entries);

                return;
            }

            if (reward.Type == BattleRewardType.Equipment)
            {
                AddEquipment(profile, reward.Id, reward.Count, configDistributor, now, entries);

                return;
            }

            if (reward.Type == BattleRewardType.Bonus)
            {
                AddBonus(profile, reward.Id, reward.Count, configDistributor, now, entries);

                return;
            }

            _logger.LogDebug("[Player] reward type {Type} is run scoped and does not change the profile", reward.Type);
        }

        private void AddBonus(
            PlayerProfileDocument profile,
            int bonusId,
            int count,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (count <= 0)
            {
                _logger.LogWarning("[Player] bonus {BonusId} count {Count} is not positive", bonusId, count);

                return;
            }

            if (configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
            {
                _logger.LogWarning("[Player] bonus {BonusId} is missing in configs", bonusId);

                return;
            }

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) == false)
            {
                _logger.LogWarning("[Player] bonus {BonusId} has unreadable work_modes {WorkModes}", bonusId, bonusMapper.WorkModeParameters);

                return;
            }

            if (IsAccountScoped(workMode) == false)
            {
                _logger.LogWarning(
                    "[Player] bonus {BonusId} with work_modes {WorkModes} lives inside a run and is not granted to the account",
                    bonusId,
                    bonusMapper.WorkModeParameters);

                return;
            }

            for (int i = 0; i < profile.Bonuses.Count; i++)
            {
                if (profile.Bonuses[i].BonusId != bonusId)
                    continue;

                profile.Bonuses[i].Count += count;
                entries.Add(CreateEntry(BonusEntry, bonusId.ToString(), count));

                return;
            }

            profile.Bonuses.Add(new PlayerBonusDocument
            {
                BonusId = bonusId,
                Count = count,
                GrantedAt = now,
            });

            entries.Add(CreateEntry(BonusEntry, bonusId.ToString(), count));
        }

        private bool IsAccountScoped(BonusWorkMode workMode)
        {
            var parts = workMode.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                var kind = parts[i].Kind;

                if (kind == BonusWorkModeKind.Permanent || kind == BonusWorkModeKind.IfEquipped)
                    continue;

                return false;
            }

            return 0 < parts.Count;
        }

        private void AddResource(PlayerProfileDocument profile, string key, long amount, List<PlayerLedgerEntryDocument> entries)
        {
            profile.Resources.TryGetValue(key, out var current);
            profile.Resources[key] = current + amount;
            entries.Add(CreateEntry(ResourceEntry, key, amount));
        }

        private void AddCharacter(
            PlayerProfileDocument profile,
            int characterId,
            int count,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (configDistributor.Characters.TryGet(characterId, out var mapper) == false)
            {
                _logger.LogWarning("[Player] character {CharacterId} is missing in configs, reward skipped", characterId);

                return;
            }

            var character = FindCharacter(profile, characterId);
            var remaining = count;

            if (character == null)
            {
                character = new PlayerCharacterDocument { ConfigId = characterId, UnlockedAt = now };
                profile.Characters.Add(character);
                remaining -= 1;
            }

            character.Copies += remaining;
            entries.Add(CreateEntry(CharacterEntry, characterId.ToString(), count));

            ApplyCharacterUpgrades(character, mapper.UpgradeCosts);

            if (mapper.UpgradeCosts.Length <= character.UpgradesApplied && 0 < character.Copies)
            {
                var overflow = character.Copies;

                character.Copies = 0;
                ApplyOverflow(profile, CharacterOverflowConstant, overflow, configDistributor, entries);
            }
        }

        private void ApplyCharacterUpgrades(PlayerCharacterDocument character, int[] upgradeCosts)
        {
            while (character.UpgradesApplied < upgradeCosts.Length)
            {
                var cost = upgradeCosts[character.UpgradesApplied];

                if (cost <= 0 || character.Copies < cost)
                    return;

                character.Copies -= cost;
                character.UpgradesApplied += 1;
            }
        }

        private void AddSummon(
            PlayerProfileDocument profile,
            int summonId,
            int count,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (configDistributor.Summons.TryGet(summonId, out _) == false)
            {
                _logger.LogWarning("[Player] summon {SummonId} is missing in configs, reward skipped", summonId);

                return;
            }

            var summon = FindSummon(profile, summonId);

            if (summon == null)
            {
                summon = new PlayerSummonDocument { ConfigId = summonId, Level = 1, UnlockedAt = now };
                profile.Summons.Add(summon);
                summon.Copies += count - 1;
            }
            else
            {
                summon.Copies += count;
            }

            entries.Add(CreateEntry(SummonEntry, summonId.ToString(), count));
        }

        private void AddEquipment(
            PlayerProfileDocument profile,
            int equipmentId,
            int count,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (configDistributor.Equipments.TryGet(equipmentId, out _) == false)
            {
                _logger.LogWarning("[Player] equipment {EquipmentId} is missing in configs, reward skipped", equipmentId);

                return;
            }

            for (int i = 0; i < count; i++)
            {
                profile.Equipment.Add(new PlayerEquipmentDocument
                {
                    InstanceId = EquipmentInstancePrefix + Guid.NewGuid().ToString("N"),
                    ConfigId = equipmentId,
                    Level = 1,
                    ObtainedAt = now,
                });
            }

            entries.Add(CreateEntry(EquipmentEntry, equipmentId.ToString(), count));
        }

        private void ApplyOverflow(
            PlayerProfileDocument profile,
            string constantKey,
            int multiplier,
            IConfigDistributor configDistributor,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (configDistributor.Constants.TryGet(constantKey, out var constant) == false)
            {
                _logger.LogWarning("[Player] constant {Constant} is missing, overflow copies dropped count = {Count}", constantKey, multiplier);

                return;
            }

            var overflowRewards = _battleRewardParser.Parse(constant.ConstantValue);

            for (int i = 0; i < overflowRewards.Count; i++)
            {
                var overflow = overflowRewards[i];

                if (overflow.Type != BattleRewardType.Resource || overflow.HasStringRewardKey == false)
                {
                    _logger.LogWarning("[Player] overflow reward {Constant} is not a resource and was skipped", constantKey);

                    continue;
                }

                AddResource(profile, overflow.RewardKey, (long)overflow.Count * multiplier, entries);
            }
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

        private PlayerLedgerEntryDocument CreateEntry(string type, string key, long amount)
        {
            return new PlayerLedgerEntryDocument { Type = type, Key = key, Amount = amount };
        }
    }
}
