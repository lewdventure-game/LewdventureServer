using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Infrastructure.Qa
{
    internal sealed class CheatPresetBuilder
    {
        public const string CharactersPreset = "characters";
        public const string SummonsPreset = "summons";
        public const string EquipmentPreset = "equipment";
        public const string ResourcesPreset = "resources";
        public const string EverythingPreset = "everything";

        private readonly ResourceKeyCollector _resourceKeyCollector;

        public CheatPresetBuilder(ResourceKeyCollector resourceKeyCollector)
        {
            _resourceKeyCollector = resourceKeyCollector;
        }

        public bool TryBuild(
            string preset,
            int amount,
            PlayerProfileDocument profile,
            IConfigDistributor configDistributor,
            List<BattleReward> rewards,
            out string error)
        {
            error = string.Empty;

            var isEverything = string.Equals(preset, EverythingPreset, StringComparison.Ordinal);
            var isKnown = isEverything
                || string.Equals(preset, CharactersPreset, StringComparison.Ordinal)
                || string.Equals(preset, SummonsPreset, StringComparison.Ordinal)
                || string.Equals(preset, EquipmentPreset, StringComparison.Ordinal)
                || string.Equals(preset, ResourcesPreset, StringComparison.Ordinal);

            if (isKnown == false)
            {
                error = $"Unknown preset {preset}.";

                return false;
            }

            if (amount < 1)
            {
                error = "Amount must be at least 1.";

                return false;
            }

            if (isEverything || string.Equals(preset, CharactersPreset, StringComparison.Ordinal))
                AddCharacters(profile, configDistributor, rewards);

            if (isEverything || string.Equals(preset, SummonsPreset, StringComparison.Ordinal))
                AddSummons(profile, configDistributor, rewards);

            if (isEverything || string.Equals(preset, EquipmentPreset, StringComparison.Ordinal))
                AddEquipment(configDistributor, rewards);

            if (isEverything || string.Equals(preset, ResourcesPreset, StringComparison.Ordinal))
                AddResources(amount, configDistributor, rewards);

            return true;
        }

        private void AddCharacters(PlayerProfileDocument profile, IConfigDistributor configDistributor, List<BattleReward> rewards)
        {
            var characters = configDistributor.Characters.Collection;

            for (int i = 0; i < characters.Count; i++)
            {
                if (OwnsCharacter(profile, characters[i].Id) == false)
                    rewards.Add(new BattleReward(BattleRewardType.Character, characters[i].Id, 1));
            }
        }

        private void AddSummons(PlayerProfileDocument profile, IConfigDistributor configDistributor, List<BattleReward> rewards)
        {
            var summons = configDistributor.Summons.Collection;

            for (int i = 0; i < summons.Count; i++)
            {
                if (OwnsSummon(profile, summons[i].Id) == false)
                    rewards.Add(new BattleReward(BattleRewardType.Summon, summons[i].Id, 1));
            }
        }

        private void AddEquipment(IConfigDistributor configDistributor, List<BattleReward> rewards)
        {
            var equipments = configDistributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
                rewards.Add(new BattleReward(BattleRewardType.Equipment, equipments[i].Id, 1));
        }

        private void AddResources(int amount, IConfigDistributor configDistributor, List<BattleReward> rewards)
        {
            var keys = _resourceKeyCollector.Collect(configDistributor);

            for (int i = 0; i < keys.Count; i++)
                rewards.Add(new BattleReward(BattleRewardType.Resource, keys[i], amount));
        }

        private bool OwnsCharacter(PlayerProfileDocument profile, int characterId)
        {
            for (int i = 0; i < profile.Characters.Count; i++)
            {
                if (profile.Characters[i].ConfigId == characterId)
                    return true;
            }

            return false;
        }

        private bool OwnsSummon(PlayerProfileDocument profile, int summonId)
        {
            for (int i = 0; i < profile.Summons.Count; i++)
            {
                if (profile.Summons[i].ConfigId == summonId)
                    return true;
            }

            return false;
        }
    }
}
