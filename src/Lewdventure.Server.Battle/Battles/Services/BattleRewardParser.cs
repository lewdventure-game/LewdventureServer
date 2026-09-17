using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    internal sealed class BattleRewardParser : IBattleRewardParser
    {
        private readonly ILogger<BattleRewardParser> _logger;

        public BattleRewardParser(ILogger<BattleRewardParser> logger)
        {
            _logger = logger;
        }

        public IReadOnlyList<BattleReward> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<BattleReward>();

            var span = value.AsSpan().Trim();

            if (2 <= span.Length && span[0] == '[' && span[span.Length - 1] == ']')
                span = span.Slice(1, span.Length - 2).Trim();

            var result = new List<BattleReward>();
            var depth = 0;
            var start = 0;

            for (int i = 0; i <= span.Length; i++)
            {
                if (i < span.Length)
                {
                    var character = span[i];

                    if (character == '[')
                    {
                        depth += 1;
                        continue;
                    }

                    if (character == ']')
                    {
                        if (0 < depth)
                            depth -= 1;

                        continue;
                    }

                    if (0 < depth)
                        continue;

                    if (character != ';' && character != ',')
                        continue;
                }

                var part = span.Slice(start, i - start).Trim();
                start = i + 1;

                if (part.IsEmpty)
                    continue;

                if (TryParseEntry(part, out var reward) == false)
                    continue;

                result.Add(reward);
            }

            return result;
        }

        public IReadOnlyList<RewardBonus> ParseBonuses(string value)
        {
            var rewards = Parse(value);

            if (rewards.Count == 0)
                return Array.Empty<RewardBonus>();

            var result = new List<RewardBonus>();

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];

                if (reward.Type != BattleRewardType.Bonus)
                    continue;

                result.Add(new RewardBonus(reward.Id, reward.Count));
            }

            return result;
        }

        private bool TryParseEntry(ReadOnlySpan<char> part, out BattleReward reward)
        {
            reward = default;

            var first = part.IndexOf(':');

            if (first < 0)
                return false;

            var typeSpan = part.Slice(0, first).Trim();
            var rest = part.Slice(first + 1);
            var second = rest.IndexOf(':');

            if (second < 0)
                return false;

            var idSpan = rest.Slice(0, second).Trim();
            var countSpan = rest.Slice(second + 1).Trim();

            if (int.TryParse(countSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) == false)
                return false;

            if (count <= 0)
                return false;

            var type = ResolveType(typeSpan);

            if (type == BattleRewardType.None)
            {
                _logger.LogWarning($"[Story][Battle]: Reward unknown type skipped, raw = {typeSpan.ToString()}, count = {count}");

                return false;
            }

            if (int.TryParse(idSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                if (id <= 0)
                {
                    _logger.LogWarning($"[Story][Battle]: Reward id invalid, type = {type}, raw = {idSpan.ToString()}, count = {count}");

                    return false;
                }

                reward = new BattleReward(type, id, count);

                return true;
            }

            if (type != BattleRewardType.Resource)
            {
                _logger.LogWarning($"[Story][Battle]: Reward parse fail non-int id, type = {type}, raw = {idSpan.ToString()}, count = {count}");

                return false;
            }

            var rewardKey = idSpan.ToString();

            if (string.IsNullOrWhiteSpace(rewardKey))
            {
                _logger.LogWarning($"[Story][Battle]: Reward resource key empty, count = {count}");

                return false;
            }

            reward = new BattleReward(type, rewardKey, count);
            _logger.LogDebug($"[Story][Battle]: Reward resource key parsed, key = {rewardKey} count = {count}");

            return true;
        }

        private BattleRewardType ResolveType(ReadOnlySpan<char> typeSpan)
        {
            if (typeSpan.Equals("bonus", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Bonus;

            if (typeSpan.Equals("status", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Status;

            if (typeSpan.Equals("resource", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Resource;

            if (typeSpan.Equals("character", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Character;

            if (typeSpan.Equals("summon", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Summon;

            if (typeSpan.Equals("equipment", StringComparison.OrdinalIgnoreCase))
                return BattleRewardType.Equipment;

            return BattleRewardType.None;
        }
    }
}
