using Server.Battles;
using Server.Logging;

namespace Server.Api.Payments
{
    internal sealed class PaymentRewardPolicy
    {
        private readonly IBattleRewardParser _parser;

        public PaymentRewardPolicy(ICoreLog coreLog)
        {
            _parser = new BattleRewardParser(coreLog);
        }

        public IReadOnlyList<BattleReward> Parse(string rewards)
        {
            return _parser.Parse(rewards);
        }

        public bool Validate(string rewards)
        {
            if (string.IsNullOrWhiteSpace(rewards))
                return true;

            if (2048 < rewards.Length)
                return false;

            var parsed = _parser.Parse(rewards);

            if (parsed.Count == 0)
                return false;

            for (var i = 0; i < parsed.Count; i++)
            {
                var reward = parsed[i];

                if (reward.Type != BattleRewardType.Resource && reward.Type != BattleRewardType.Account)
                    return false;

                if (reward.HasStringRewardKey == false || reward.Count <= 0 || IsIdentifier(reward.RewardKey) == false)
                    return false;
            }

            return true;
        }

        private bool IsIdentifier(string value)
        {
            if (value.Length == 0 || 64 < value.Length)
                return false;

            foreach (var character in value)
            {
                if (char.IsAsciiLetterOrDigit(character) == false && character != '-' && character != '_')
                    return false;
            }

            return true;
        }
    }
}
