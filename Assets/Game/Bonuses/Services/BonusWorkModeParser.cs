using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Server.Bonuses
{
    internal sealed class BonusWorkModeParser : IBonusWorkModeParser
    {
        private readonly ILogger<BonusWorkModeParser> _logger;

        public BonusWorkModeParser(ILogger<BonusWorkModeParser> logger)
        {
            _logger = logger;
        }

        public BonusWorkMode Parse(string raw)
        {
            var workMode = ParseCore(raw);

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning($"[Config] work_mode empty; kind = {BonusWorkModeKind.Unknown}");

                return workMode;
            }

            if (workMode.Kind == BonusWorkModeKind.Unknown)
            {
                _logger.LogWarning($"[Config] work_mode unknown raw = {raw.Trim()}");

                return workMode;
            }

            if (workMode.Kind == BonusWorkModeKind.IfEquipped)
                _logger.LogDebug($"[Config] work_mode parsed kind = {workMode.Kind} entityType = {workMode.EquippedEntityType} entityId = {workMode.EquippedEntityId}");
            else if (workMode.Kind == BonusWorkModeKind.NextBattles || workMode.Kind == BonusWorkModeKind.FirstTurns)
                _logger.LogDebug($"[Config] work_mode parsed kind = {workMode.Kind} count = {workMode.Count}");
            else
                _logger.LogDebug($"[Config] work_mode parsed kind = {workMode.Kind}");

            return workMode;
        }

        public static BonusWorkMode ParseCore(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return BonusWorkMode.Unknown;

            var trimmed = raw.Trim();

            if (trimmed.Equals("permanent", StringComparison.OrdinalIgnoreCase))
                return new BonusWorkMode(BonusWorkModeKind.Permanent, string.Empty, 0, 0);

            if (trimmed.Equals("end_of_game", StringComparison.OrdinalIgnoreCase))
                return new BonusWorkMode(BonusWorkModeKind.EndOfGame, string.Empty, 0, 0);

            if (trimmed.Equals("end_of_battle", StringComparison.OrdinalIgnoreCase))
                return new BonusWorkMode(BonusWorkModeKind.EndOfBattle, string.Empty, 0, 0);

            if (trimmed.Equals("every_turn", StringComparison.OrdinalIgnoreCase))
                return new BonusWorkMode(BonusWorkModeKind.EveryTurn, string.Empty, 0, 0);

            if (TryParsePrefixedCount(trimmed, "next_battles", out var nextBattlesCount))
                return new BonusWorkMode(BonusWorkModeKind.NextBattles, string.Empty, 0, nextBattlesCount);

            if (TryParsePrefixedCount(trimmed, "first_turns", out var firstTurnsCount))
                return new BonusWorkMode(BonusWorkModeKind.FirstTurns, string.Empty, 0, firstTurnsCount);

            if (TryParseIfEquipped(trimmed, out var entityType, out var entityId))
                return new BonusWorkMode(BonusWorkModeKind.IfEquipped, entityType, entityId, 0);

            return BonusWorkMode.Unknown;
        }

        private static bool TryParsePrefixedCount(string raw, string prefix, out int count)
        {
            count = 0;
            var expectedPrefix = prefix + ":";

            if (raw.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            var countRaw = raw.Substring(expectedPrefix.Length);

            return int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
        }

        private static bool TryParseIfEquipped(string raw, out string entityType, out int entityId)
        {
            entityType = string.Empty;
            entityId = 0;
            const string prefix = "if_equipped:";

            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            var remainder = raw.Substring(prefix.Length);
            var separator = remainder.IndexOf(':');

            if (separator <= 0)
                return false;

            entityType = remainder.Substring(0, separator).Trim();
            var idRaw = remainder.Substring(separator + 1).Trim();

            if (string.IsNullOrEmpty(entityType))
                return false;

            return int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId);
        }
    }
}
