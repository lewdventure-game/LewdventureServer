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
            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogError("[Config]: work_mode empty");

                throw new InvalidOperationException("[Config]: work_mode empty");
            }

            var trimmed = raw.Trim();

            if (trimmed.Equals("permanent", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.Permanent, string.Empty, 0, 0));

            if (trimmed.Equals("end_of_game", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.EndOfGame, string.Empty, 0, 0));

            if (trimmed.Equals("end_of_battle", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.EndOfBattle, string.Empty, 0, 0));

            if (trimmed.Equals("every_turn", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.EveryTurn, string.Empty, 0, 0));

            if (trimmed.StartsWith("next_battles:", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.NextBattles, string.Empty, 0, ParsePrefixedCount(trimmed, "next_battles")));

            if (trimmed.StartsWith("first_turns:", StringComparison.OrdinalIgnoreCase))
                return LogParsed(new BonusWorkMode(BonusWorkModeKind.FirstTurns, string.Empty, 0, ParsePrefixedCount(trimmed, "first_turns")));

            if (trimmed.StartsWith("if_equipped:", StringComparison.OrdinalIgnoreCase))
            {
                ParseIfEquipped(trimmed, out var entityType, out var entityId);

                return LogParsed(new BonusWorkMode(BonusWorkModeKind.IfEquipped, entityType, entityId, 0));
            }

            _logger.LogError($"[Config]: work_mode unknown, raw = {trimmed}");

            throw new InvalidOperationException($"[Config]: work_mode unknown, raw = {trimmed}");
        }

        private BonusWorkMode LogParsed(BonusWorkMode workMode)
        {
            if (workMode.Kind == BonusWorkModeKind.IfEquipped)
                _logger.LogDebug($"[Config]: work_mode parsed, kind = {workMode.Kind}, entityType = {workMode.EquippedEntityType}, entityId = {workMode.EquippedEntityId}");
            else if (workMode.Kind == BonusWorkModeKind.NextBattles || workMode.Kind == BonusWorkModeKind.FirstTurns)
                _logger.LogDebug($"[Config]: work_mode parsed, kind = {workMode.Kind}, count = {workMode.Count}");
            else
                _logger.LogDebug($"[Config]: work_mode parsed, kind = {workMode.Kind}");

            return workMode;
        }

        private int ParsePrefixedCount(string raw, string prefix)
        {
            var expectedPrefix = prefix + ":";
            var countRaw = raw.Substring(expectedPrefix.Length);

            if (int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) == false)
            {
                _logger.LogError($"[Config]: work_mode {prefix} count invalid, raw = {raw}");

                throw new InvalidOperationException($"[Config]: work_mode {prefix} count invalid, raw = {raw}");
            }

            if (count <= 0)
            {
                _logger.LogError($"[Config]: work_mode {prefix} count <= 0, raw = {raw}");

                throw new InvalidOperationException($"[Config]: work_mode {prefix} count <= 0, raw = {raw}");
            }

            return count;
        }

        private void ParseIfEquipped(string raw, out string entityType, out int entityId)
        {
            const string prefix = "if_equipped:";
            var remainder = raw.Substring(prefix.Length);
            var separator = remainder.IndexOf(':');

            if (separator <= 0)
            {
                _logger.LogError($"[Config]: if_equipped format invalid, raw = {raw}");

                throw new InvalidOperationException($"[Config]: if_equipped format invalid, raw = {raw}");
            }

            entityType = remainder.Substring(0, separator).Trim();
            var idRaw = remainder.Substring(separator + 1).Trim();

            if (int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId) == false
                || entityId <= 0)
            {
                _logger.LogError($"[Config]: if_equipped entity id invalid, raw = {raw}");

                throw new InvalidOperationException($"[Config]: if_equipped entity id invalid, raw = {raw}");
            }

            if (IsAllowedEquippedEntityType(entityType) == false)
            {
                _logger.LogError($"[Config]: if_equipped entity type unsupported, entityType = {entityType}, raw = {raw}");

                throw new InvalidOperationException($"[Config]: if_equipped entity type unsupported, entityType = {entityType}");
            }
        }

        private bool IsAllowedEquippedEntityType(string entityType)
        {
            if (entityType.Equals("equipments", StringComparison.OrdinalIgnoreCase))
                return true;

            if (entityType.Equals("characters", StringComparison.OrdinalIgnoreCase))
                return true;

            if (entityType.Equals("summons", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
