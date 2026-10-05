using Server.Battles;
using Server.Services;

namespace Server.Infrastructure.Qa
{
    internal sealed class ResourceKeyCollector
    {
        private const string ResourceType = "resource";
        private const string ResourcePrefix = "resource:";
        private const string RunExperienceResource = "exp_lvl";

        private readonly IBattleRewardParser _battleRewardParser;

        public ResourceKeyCollector(IBattleRewardParser battleRewardParser)
        {
            _battleRewardParser = battleRewardParser;
        }

        public List<string> Collect(IConfigDistributor configDistributor)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            var summonLevels = configDistributor.SummonLevels.Collection;

            for (int i = 0; i < summonLevels.Count; i++)
                AddCosts(keys, summonLevels[i].ResourceTypes, summonLevels[i].ResourceIds);

            var skillPromotes = configDistributor.SkillPromotes.Collection;

            for (int i = 0; i < skillPromotes.Count; i++)
                AddCosts(keys, skillPromotes[i].ResourceTypes, skillPromotes[i].ResourceIds);

            var equipmentPromotes = configDistributor.EquipmentPromotes.Collection;

            for (int i = 0; i < equipmentPromotes.Count; i++)
                AddCosts(keys, equipmentPromotes[i].ResourceTypes, equipmentPromotes[i].ResourceIds);

            var characterPromotes = configDistributor.CharacterPromotes.Collection;

            for (int i = 0; i < characterPromotes.Count; i++)
                AddRewards(keys, characterPromotes[i].RewardTypes, characterPromotes[i].RewardIds);

            var summonMasteries = configDistributor.SummonMasteries.Collection;

            for (int i = 0; i < summonMasteries.Count; i++)
                AddRewards(keys, summonMasteries[i].RewardTypes, summonMasteries[i].RewardIds);

            var constants = configDistributor.Constants.Collection;

            for (int i = 0; i < constants.Count; i++)
            {
                var value = constants[i].ConstantValue;

                if (string.IsNullOrEmpty(value) || value.Contains(ResourcePrefix, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var rewards = _battleRewardParser.Parse(value);

                for (int j = 0; j < rewards.Count; j++)
                {
                    if (rewards[j].Type == BattleRewardType.Resource && rewards[j].HasStringRewardKey)
                        Add(keys, rewards[j].RewardKey);
                }
            }

            var result = new List<string>(keys);

            result.Sort(StringComparer.Ordinal);

            return result;
        }

        private void AddCosts(HashSet<string> keys, string[] types, string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var type = i < types.Length ? types[i].Trim() : ResourceType;

                if (type.Length != 0 && string.Equals(type, ResourceType, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                Add(keys, ids[i]);
            }
        }

        private void AddRewards(HashSet<string> keys, string[] types, string[] ids)
        {
            var count = types.Length < ids.Length ? types.Length : ids.Length;

            for (int i = 0; i < count; i++)
            {
                if (string.Equals(types[i].Trim(), ResourceType, StringComparison.OrdinalIgnoreCase))
                    Add(keys, ids[i]);
            }
        }

        private void Add(HashSet<string> keys, string rawKey)
        {
            var key = rawKey.Trim();

            if (key.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
                key = key.Substring(ResourcePrefix.Length).Trim();

            if (key.Length == 0 || string.Equals(key, RunExperienceResource, StringComparison.Ordinal))
                return;

            keys.Add(key);
        }
    }
}
