using System.Globalization;

namespace Server.Infrastructure.Players
{
    internal sealed class DialogueLaunchKeys
    {
        private const string LevelPrefix = "level:";
        private const string RewardPrefix = "reward:";
        private const char Separator = ':';

        public string ForLevel(int storyLevelId, int dialogueId, string trigger)
        {
            return LevelPrefix
                + storyLevelId.ToString(CultureInfo.InvariantCulture)
                + Separator
                + dialogueId.ToString(CultureInfo.InvariantCulture)
                + Separator
                + NormalizeTrigger(trigger);
        }

        public string NormalizeTrigger(string trigger)
        {
            return trigger.Trim().ToLowerInvariant();
        }

        public string ForReward(int dialogueId)
        {
            return RewardPrefix
                + dialogueId.ToString(CultureInfo.InvariantCulture)
                + Separator
                + Guid.NewGuid().ToString("N");
        }

        public bool IsReward(string launchKey)
        {
            return launchKey.StartsWith(RewardPrefix, StringComparison.Ordinal);
        }

        public bool TryParseLevel(string launchKey, out int storyLevelId, out int dialogueId, out string trigger)
        {
            storyLevelId = 0;
            dialogueId = 0;
            trigger = string.Empty;

            if (launchKey.StartsWith(LevelPrefix, StringComparison.Ordinal) == false)
                return false;

            var parts = launchKey.Substring(LevelPrefix.Length).Split(Separator, 3);

            if (parts.Length != 3)
                return false;

            if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out storyLevelId) == false)
                return false;

            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out dialogueId) == false)
                return false;

            trigger = parts[2];

            return trigger.Length != 0;
        }
    }
}
