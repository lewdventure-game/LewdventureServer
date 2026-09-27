using System.Globalization;
using Server.Configs;
using Server.Perks;

namespace Server.Battles
{
    internal sealed class PerkParameterReader
    {
        private readonly IBattleParameterParser _battleParameterParser;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly ICoreLog _coreLog;
        private readonly ParserUtils _parserUtils;
        private readonly Dictionary<string, string> _parameterCache = new(16, StringComparer.OrdinalIgnoreCase);

        public PerkParameterReader(
            IBattleParameterParser battleParameterParser,
            IBattleRewardParser battleRewardParser,
            ICoreLog coreLog,
            ParserUtils parserUtils)
        {
            _battleParameterParser = battleParameterParser;
            _battleRewardParser = battleRewardParser;
            _coreLog = coreLog;
            _parserUtils = parserUtils;
        }

        public void Read(IPerkMapper mapper)
        {
            _battleParameterParser.ParseKeyValues(mapper.PerkParameters, _parameterCache);
        }

        public IReadOnlyList<BattleReward> Rewards(string key)
        {
            return _parameterCache.TryGetValue(key, out var value)
                ? _battleRewardParser.Parse(value)
                : [];
        }

        public IReadOnlyList<int> ProcRounds()
        {
            return _parameterCache.TryGetValue("proc_rounds", out var value)
                ? _parserUtils.ParseIntList(value)
                : [];
        }

        public float Float(string key, float defaultValue)
        {
            return _parameterCache.TryGetValue(key, out var value)
                ? _parserUtils.GetFloat(value, defaultValue)
                : defaultValue;
        }

        public int Int(string key, int defaultValue)
        {
            return _parameterCache.TryGetValue(key, out var value)
                ? _parserUtils.GetInt(value, defaultValue)
                : defaultValue;
        }

        public IReadOnlyList<PerkActionThreshold> ActionThresholds()
        {
            if (_parameterCache.TryGetValue("actions", out var raw) == false || string.IsNullOrWhiteSpace(raw))
                return [];

            var parts = raw.Split(SeparatorFormat.ListSeparator);
            var result = new List<PerkActionThreshold>();

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var separator = part.IndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator < 0)
                {
                    _coreLog.Error($"[Story][Battle]: Perk action pair invalid '{part}'");

                    continue;
                }

                var actionKey = part.Substring(0, separator).Trim();
                var thresholdRaw = part.Substring(separator + 1).Trim();

                if (TryParseActionType(actionKey, out var actionType) == false)
                {
                    _coreLog.Error($"[Story][Battle]: Perk action unknown '{actionKey}'");

                    continue;
                }

                if (int.TryParse(thresholdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) == false || threshold <= 0)
                {
                    _coreLog.Error($"[Story][Battle]: Perk action threshold invalid '{thresholdRaw}'");

                    continue;
                }

                result.Add(new PerkActionThreshold(actionType, threshold));
            }

            return result;
        }

        private bool TryParseActionType(string key, out BattlePerkActionType actionType)
        {
            switch (key)
            {
                case "attack":
                    actionType = BattlePerkActionType.Attack;

                    return true;
                case "counterattack":
                    actionType = BattlePerkActionType.CounterAttack;

                    return true;
                case "comboattack":
                    actionType = BattlePerkActionType.ComboAttack;

                    return true;
                case "any_damage":
                    actionType = BattlePerkActionType.AnyDamage;

                    return true;
                default:
                    actionType = BattlePerkActionType.None;

                    return false;
            }
        }
    }
}
