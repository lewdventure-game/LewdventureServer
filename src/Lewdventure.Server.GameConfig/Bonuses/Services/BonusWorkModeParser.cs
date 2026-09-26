using System.Globalization;
using Microsoft.Extensions.Logging;
using Server.Configs;

namespace Server.Bonuses
{
    internal sealed class BonusWorkModeParser : IBonusWorkModeParser
    {
        private readonly ILogger<BonusWorkModeParser> _logger;

        public BonusWorkModeParser(ILogger<BonusWorkModeParser> logger)
        {
            _logger = logger;
        }

        public bool TryParse(string raw, out BonusWorkMode workMode)
        {
            workMode = new BonusWorkMode(new List<BonusWorkModePart>());

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogError("[Config]: work_mode empty");

                return false;
            }

            var segments = SplitSegments(raw.Trim());

            if (segments.Count == 0)
            {
                _logger.LogError("[Config]: work_mode empty");

                return false;
            }

            var parts = new List<BonusWorkModePart>(segments.Count);

            for (int i = 0; i < segments.Count; i++)
            {
                if (TryParseSingle(segments[i], out var part) == false)
                    return false;

                parts.Add(part);
            }

            workMode = new BonusWorkMode(parts);
            LogParsed(workMode);

            return true;
        }

        private List<string> SplitSegments(string raw)
        {
            var parts = raw.Split(SeparatorFormat.PairSeparator);
            var segments = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                var segment = parts[i].Trim();

                if (string.IsNullOrEmpty(segment))
                    continue;

                segments.Add(segment);
            }

            return segments;
        }

        private bool TryParseSingle(string trimmed, out BonusWorkModePart part)
        {
            part = new BonusWorkModePart(BonusWorkModeKind.Unknown, string.Empty, 0, 0);

            if (trimmed.Equals("permanent", StringComparison.OrdinalIgnoreCase))
            {
                part = new BonusWorkModePart(BonusWorkModeKind.Permanent, string.Empty, 0, 0);

                return true;
            }

            if (trimmed.Equals("end_of_game", StringComparison.OrdinalIgnoreCase))
            {
                part = new BonusWorkModePart(BonusWorkModeKind.EndOfGame, string.Empty, 0, 0);

                return true;
            }

            if (trimmed.Equals("end_of_battle", StringComparison.OrdinalIgnoreCase))
            {
                part = new BonusWorkModePart(BonusWorkModeKind.EndOfBattle, string.Empty, 0, 0);

                return true;
            }

            if (trimmed.Equals("every_turn", StringComparison.OrdinalIgnoreCase))
            {
                part = new BonusWorkModePart(BonusWorkModeKind.EveryTurn, string.Empty, 0, 0);

                return true;
            }

            if (trimmed.StartsWith("next_battles:", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParsePrefixedCount(trimmed, "next_battles", out var nextBattlesCount) == false)
                    return false;

                part = new BonusWorkModePart(BonusWorkModeKind.NextBattles, string.Empty, 0, nextBattlesCount);

                return true;
            }

            if (trimmed.StartsWith("first_turns:", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParsePrefixedCount(trimmed, "first_turns", out var firstTurnsCount) == false)
                    return false;

                part = new BonusWorkModePart(BonusWorkModeKind.FirstTurns, string.Empty, 0, firstTurnsCount);

                return true;
            }

            if (trimmed.StartsWith("if_equipped:", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseIfEquipped(trimmed, out var entityType, out var entityId) == false)
                    return false;

                part = new BonusWorkModePart(BonusWorkModeKind.IfEquipped, entityType, entityId, 0);

                return true;
            }

            _logger.LogError($"[Config]: work_mode unknown, raw = {trimmed}");

            return false;
        }

        private void LogParsed(BonusWorkMode workMode)
        {
            _logger.LogDebug($"[Config]: work_mode parsed, modes = {workMode.Format()}");
        }

        private bool TryParsePrefixedCount(string raw, string prefix, out int count)
        {
            var expectedPrefix = prefix + SeparatorFormat.KeyValueSeparator;
            var countRaw = raw.Substring(expectedPrefix.Length);

            if (int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) == false)
            {
                _logger.LogError($"[Config]: work_mode {prefix} count invalid, raw = {raw}");

                return false;
            }

            if (count <= 0)
            {
                _logger.LogError($"[Config]: work_mode {prefix} count <= 0, raw = {raw}");

                return false;
            }

            return true;
        }

        private bool TryParseIfEquipped(string raw, out string entityType, out int entityId)
        {
            entityType = string.Empty;
            entityId = 0;

            var prefix = "if_equipped" + SeparatorFormat.KeyValueSeparator;
            var remainder = raw.Substring(prefix.Length);
            var separator = remainder.IndexOf(SeparatorFormat.KeyValueSeparator);

            if (separator <= 0)
            {
                _logger.LogError($"[Config]: if_equipped format invalid, raw = {raw}");

                return false;
            }

            entityType = remainder.Substring(0, separator).Trim();
            var idRaw = remainder.Substring(separator + 1).Trim();

            if (int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId) == false
                || entityId <= 0)
            {
                _logger.LogError($"[Config]: if_equipped entity id invalid, raw = {raw}");

                return false;
            }

            if (IsAllowedEquippedEntityType(entityType) == false)
            {
                _logger.LogError($"[Config]: if_equipped entity type unsupported, entityType = {entityType}, raw = {raw}");

                return false;
            }

            return true;
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
