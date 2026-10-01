using Server.Battles;
using Server.Common;
using Server.Entities;
using Server.Bonuses;
using Server.Configs;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class RewardApplier
    {
        private const string CharacterOverflowConstant = "character_overflow_resource";
        private const string RareSummonOverflowConstant = "rare_summon_overflow_resource";
        private const string EpicSummonOverflowConstant = "epic_summon_overflow_resource";
        private const string LegendarySummonOverflowConstant = "legendary_summon_overflow_resource";
        private const string MythicSummonOverflowConstant = "mythic_summon_overflow_resource";
        private const string EquipmentInstancePrefix = "eq_";
        private const string ResourceEntry = "resource";
        private const string CharacterEntry = "character";
        private const string SummonEntry = "summon";
        private const string EquipmentEntry = "equipment";
        private const string FlagEntry = "flag";
        private const string BonusEntry = "bonus";
        private const string PromoteEntry = "promote";
        private const string SceneEntry = "scene";
        private const int NoSceneOwner = 0;

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
                ApplyReward(profile, rewards[i], NoSceneOwner, configDistributor, now, entries);

            return entries;
        }

        private void ApplyReward(
            PlayerProfileDocument profile,
            in BattleReward reward,
            int sceneOwnerId,
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

            if (reward.Type == BattleRewardType.Story)
            {
                AddStoryScene(profile, reward.Id, sceneOwnerId, entries);

                return;
            }

            _logger.LogDebug("[Player] reward type {Type} is run scoped and does not change the profile", reward.Type);
        }

        private void AddStoryScene(PlayerProfileDocument profile, int sceneId, int sceneOwnerId, List<PlayerLedgerEntryDocument> entries)
        {
            if (sceneId <= 0)
            {
                _logger.LogWarning("[Player] story scene id {SceneId} is not positive", sceneId);

                return;
            }

            if (sceneOwnerId <= 0)
            {
                _logger.LogWarning("[Player] story scene {SceneId} has no character context and is not unlocked", sceneId);

                return;
            }

            var character = FindCharacter(profile, sceneOwnerId);

            if (character == null)
            {
                _logger.LogWarning("[Player] story scene {SceneId} owner {CharacterId} is not owned", sceneId, sceneOwnerId);

                return;
            }

            if (character.UnlockedSceneIds.Contains(sceneId))
            {
                _logger.LogDebug("[Player] story scene {SceneId} is already unlocked for character {CharacterId}", sceneId, sceneOwnerId);

                return;
            }

            character.UnlockedSceneIds.Add(sceneId);
            entries.Add(CreateEntry(SceneEntry, $"{sceneOwnerId}:{sceneId}", 1));

            _logger.LogInformation("[Player] story scene {SceneId} unlocked for character {CharacterId}", sceneId, sceneOwnerId);
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

            ApplyCharacterPromotes(profile, character, mapper, configDistributor, now, entries);

            var maxPromoteLevel = configDistributor.CharacterPromotes.GetMaxPromoteLevel(mapper.PromoteId);

            if (0 < maxPromoteLevel && maxPromoteLevel <= character.UpgradesApplied && 0 < character.Copies)
            {
                var overflow = character.Copies;

                character.Copies = 0;
                ApplyOverflow(profile, CharacterOverflowConstant, overflow, configDistributor, entries);
            }
        }

        private void ApplyCharacterPromotes(
            PlayerProfileDocument profile,
            PlayerCharacterDocument character,
            ICharacterMapper mapper,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (mapper.PromoteId <= 0)
            {
                _logger.LogWarning("[Player] character {CharacterId} has no promote_id, copies stay unspent", mapper.Id);

                return;
            }

            var promotes = configDistributor.CharacterPromotes;

            if (promotes.Collection.Count == 0)
            {
                _logger.LogWarning("[Player] Character_promotes is empty, character {CharacterId} copies stay unspent", mapper.Id);

                return;
            }

            while (true)
            {
                var nextLevel = character.UpgradesApplied + 1;

                if (promotes.TryGet(mapper.PromoteId, nextLevel, out var promote) == false)
                    return;

                if (promote.CopiesToUpgrade <= 0)
                {
                    _logger.LogWarning(
                        "[Player] promote {PromoteId} level {Level} has copies_to_upgrade {Copies}, promotion stopped",
                        mapper.PromoteId,
                        nextLevel,
                        promote.CopiesToUpgrade);

                    return;
                }

                if (character.Copies < promote.CopiesToUpgrade)
                    return;

                character.Copies -= promote.CopiesToUpgrade;
                character.UpgradesApplied = nextLevel;
                entries.Add(CreateEntry(PromoteEntry, $"{mapper.Id}:{nextLevel}", promote.CopiesToUpgrade));

                _logger.LogInformation(
                    "[Player] character {CharacterId} promoted to level {Level} for {Copies} copies",
                    mapper.Id,
                    nextLevel,
                    promote.CopiesToUpgrade);

                ApplyPromoteRewards(profile, promote, mapper.Id, configDistributor, now, entries);
            }
        }

        private void ApplyPromoteRewards(
            PlayerProfileDocument profile,
            ICharacterPromoteMapper promote,
            int sceneOwnerId,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            var rewards = BuildPromoteRewards(promote);

            if (rewards.Count == 0)
                return;

            for (int i = 0; i < rewards.Count; i++)
                ApplyReward(profile, rewards[i], sceneOwnerId, configDistributor, now, entries);
        }

        private IReadOnlyList<BattleReward> BuildPromoteRewards(ICharacterPromoteMapper promote)
        {
            var types = promote.RewardTypes;
            var ids = promote.RewardIds;
            var values = promote.RewardValues;

            if (types.Length == 0)
                return Array.Empty<BattleReward>();

            if (types.Length != ids.Length || types.Length != values.Length)
            {
                _logger.LogWarning(
                    "[Player] promote {PromoteId} level {Level} has {Types} types, {Ids} ids and {Values} values, rewards skipped",
                    promote.Id,
                    promote.PromoteLevel,
                    types.Length,
                    ids.Length,
                    values.Length);

                return Array.Empty<BattleReward>();
            }

            var builder = new System.Text.StringBuilder();

            for (int i = 0; i < types.Length; i++)
            {
                if (0 < builder.Length)
                    builder.Append(',');

                builder.Append(types[i]);
                builder.Append(':');
                builder.Append(ids[i]);
                builder.Append(':');
                builder.Append(values[i]);
            }

            return _battleRewardParser.Parse(builder.ToString());
        }

        private void AddSummon(
            PlayerProfileDocument profile,
            int summonId,
            int count,
            IConfigDistributor configDistributor,
            DateTime now,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (configDistributor.Summons.TryGet(summonId, out var summonMapper) == false)
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
            ApplySummonCopiesOverflow(profile, summon, summonMapper, configDistributor, entries);
        }

        private void ApplySummonCopiesOverflow(
            PlayerProfileDocument profile,
            PlayerSummonDocument summon,
            ISummonMapper summonMapper,
            IConfigDistributor configDistributor,
            List<PlayerLedgerEntryDocument> entries)
        {
            if (summon.Copies <= 0)
                return;

            var maxMasteryLevel = configDistributor.Masteries.GetMaxMasteryLevel(summonMapper.MasteryId);

            if (maxMasteryLevel <= 0 || summon.MasteryLevel < maxMasteryLevel)
                return;

            if (TryResolveSummonOverflowConstant(summonMapper.Rarity, out var constantKey) == false)
            {
                _logger.LogWarning(
                    "[Player] summon {SummonId} rarity {Rarity} has no overflow constant, {Copies} copies stay unspent",
                    summonMapper.Id,
                    summonMapper.Rarity,
                    summon.Copies);

                return;
            }

            var overflow = summon.Copies;

            summon.Copies = 0;
            ApplyOverflow(profile, constantKey, overflow, configDistributor, entries);

            _logger.LogInformation(
                "[Player] summon {SummonId} copies converted after max mastery {Mastery}, copies = {Copies}",
                summonMapper.Id,
                maxMasteryLevel,
                overflow);
        }

        private bool TryResolveSummonOverflowConstant(RarityType rarity, out string constantKey)
        {
            if (rarity == RarityType.Rare)
            {
                constantKey = RareSummonOverflowConstant;

                return true;
            }

            if (rarity == RarityType.Epic)
            {
                constantKey = EpicSummonOverflowConstant;

                return true;
            }

            if (rarity == RarityType.Legendary)
            {
                constantKey = LegendarySummonOverflowConstant;

                return true;
            }

            if (rarity == RarityType.Mythic)
            {
                constantKey = MythicSummonOverflowConstant;

                return true;
            }

            constantKey = string.Empty;

            return false;
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
