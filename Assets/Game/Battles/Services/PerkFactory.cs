using System.Globalization;
using Microsoft.Extensions.Logging;
using Server.Configs;
using Server.Perks;

namespace Server.Battles
{
    internal sealed class PerkFactory : IPerkFactory
    {
        private readonly ILogger<PerkFactory> _logger;
        private readonly IBattleParameterParser _battleParameterParser;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IBattleRewardService _battleRewardService;
        private readonly Dictionary<string, string> _parameterCache = new(16, StringComparer.OrdinalIgnoreCase);

        public PerkFactory(
            ILogger<PerkFactory> logger,
            IBattleParameterParser battleParameterParser,
            IBattleRewardParser battleRewardParser,
            IBattleRewardService battleRewardService)
        {
            _logger = logger;
            _battleParameterParser = battleParameterParser;
            _battleRewardParser = battleRewardParser;
            _battleRewardService = battleRewardService;
        }

        public IPerk Create(IPerkMapper mapper)
        {
            switch (mapper.PerkType)
            {
                case PerkType.Reward:
                    return CreateRewardPerk(mapper);
                case PerkType.FireAttack:
                    return CreateElementalPerk(mapper, PerkType.FireAttack);
                case PerkType.EarthAttack:
                    return CreateElementalPerk(mapper, PerkType.EarthAttack);
                case PerkType.AirAttack:
                    return CreateElementalPerk(mapper, PerkType.AirAttack);
                case PerkType.WaterAttack:
                    return CreateElementalPerk(mapper, PerkType.WaterAttack);
                case PerkType.ActionReward:
                    return CreateActionRewardPerk(mapper);
                case PerkType.Resurrection:
                    return CreateResurrectionPerk(mapper);
                default:
                    _logger.LogError($"[Story][Battle] perk unknown type id = {mapper.Id}, type = {mapper.PerkType}");

                    return new UnknownPerk(mapper, _logger);
            }
        }

        private IPerk CreateRewardPerk(IPerkMapper mapper)
        {
            _battleParameterParser.ParseKeyValues(mapper.PerkParameters, _parameterCache);
            var rewards = ParseRewardsKey("rewards");

            return new RewardPerk(mapper, rewards, _battleRewardService, _logger);
        }

        private IPerk CreateActionRewardPerk(IPerkMapper mapper)
        {
            _battleParameterParser.ParseKeyValues(mapper.PerkParameters, _parameterCache);
            var rewardsOnGrant = ParseRewardsKey("rewards");
            var rewardsOnAction = ParseRewardsKey("rewards_on_action");
            var thresholds = ParseActionThresholds();
            var rewardsChance = GetFloat("rewards_chance", 0f);

            return new ActionRewardPerk(
                mapper,
                rewardsOnGrant,
                rewardsOnAction,
                thresholds,
                rewardsChance,
                _battleRewardService,
                _logger);
        }

        private IPerk CreateResurrectionPerk(IPerkMapper mapper)
        {
            _battleParameterParser.ParseKeyValues(mapper.PerkParameters, _parameterCache);
            var healthRatio = GetFloat("health_ratio", 0f);
            var resurrectionsCount = GetInt("resurrections_count", 0);

            if (healthRatio <= 0f || resurrectionsCount <= 0)
                _logger.LogError($"[Story][Battle] perk resurrection invalid params id = {mapper.Id}, healthRatio = {healthRatio}, count = {resurrectionsCount}");

            return new ResurrectionPerk(mapper, healthRatio, resurrectionsCount, _logger);
        }

        private IPerk CreateElementalPerk(IPerkMapper mapper, PerkType perkType)
        {
            _battleParameterParser.ParseKeyValues(mapper.PerkParameters, _parameterCache);

            var projectileCount = GetInt("projectile_count", 1);
            var damageRatio = GetFloat("damage_ratio", 0f);
            var hitRewards = ParseRewardsKey("hit_rewards");
            var rewardsChance = GetFloat("rewards_chance", 0f);
            var debuffRewardsChance = GetFloat("debuff_rewards_chance", 1f);
            var procRounds = ParseProcRounds();

            if (procRounds.Count == 0)
                _logger.LogWarning($"[Story][Battle] perk elemental proc_rounds empty perkId = {mapper.Id} type = {perkType}");

            if (damageRatio <= 0f)
                _logger.LogError($"[Story][Battle] perk elemental invalid damage_ratio id = {mapper.Id}, type = {perkType}");

            switch (perkType)
            {
                case PerkType.FireAttack:
                    return new FireAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
                case PerkType.EarthAttack:
                    return new EarthAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
                case PerkType.AirAttack:
                    return new AirAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
                case PerkType.WaterAttack:
                    return new WaterAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
                default:
                    return new UnknownPerk(mapper, _logger);
            }
        }

        private IReadOnlyList<BattleReward> ParseRewardsKey(string key)
        {
            if (_parameterCache.TryGetValue(key, out var value) == false)
                return Array.Empty<BattleReward>();

            return _battleRewardParser.Parse(value);
        }

        private IReadOnlyList<int> ParseProcRounds()
        {
            if (_parameterCache.TryGetValue("proc_rounds", out var value) == false)
                return Array.Empty<int>();

            return ParserUtils.ParseIntList(value);
        }

        private IReadOnlyList<PerkActionThreshold> ParseActionThresholds()
        {
            if (_parameterCache.TryGetValue("actions", out var raw) == false || string.IsNullOrWhiteSpace(raw))
                return Array.Empty<PerkActionThreshold>();

            var parts = raw.Split(',');
            var result = new List<PerkActionThreshold>();

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var separator = part.IndexOf(':');

                if (separator < 0)
                {
                    _logger.LogError($"[Story][Battle] perk action pair invalid '{part}'");

                    continue;
                }

                var actionKey = part.Substring(0, separator).Trim();
                var thresholdRaw = part.Substring(separator + 1).Trim();

                if (string.Equals(actionKey, "atack", StringComparison.OrdinalIgnoreCase))
                    actionKey = "attack";

                if (TryParseActionType(actionKey, out var actionType) == false)
                {
                    _logger.LogError($"[Story][Battle] perk action unknown '{actionKey}'");

                    continue;
                }

                if (int.TryParse(thresholdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) == false || threshold <= 0)
                {
                    _logger.LogError($"[Story][Battle] perk action threshold invalid '{thresholdRaw}'");

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

        private float GetFloat(string key, float defaultValue)
        {
            if (_parameterCache.TryGetValue(key, out var value) == false)
                return defaultValue;

            return ParserUtils.GetFloat(value, defaultValue);
        }

        private int GetInt(string key, int defaultValue)
        {
            if (_parameterCache.TryGetValue(key, out var value) == false)
                return defaultValue;

            return ParserUtils.GetInt(value, defaultValue);
        }
    }
}
