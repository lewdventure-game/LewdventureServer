using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class SkillFactory : ISkillFactory
    {
        private readonly ILogger<SkillFactory> _logger;
        private readonly IConfigDistributor _configDistributor;

        public SkillFactory(
            ILogger<SkillFactory> logger,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _configDistributor = configDistributor;
        }

        public ISkill Create(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                _logger.LogWarning($"[Story][Battle]: Skill unknown, id = {skillId}");

                return new UnknownSkill(new SkillMapper(0, string.Empty, SkillType.Unknown, string.Empty));
            }

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
            {
                _logger.LogWarning($"[Story][Battle]: Skill unknown, id = {trimmed}");

                return new UnknownSkill(new SkillMapper(0, trimmed, SkillType.Unknown, string.Empty));
            }

            if (TryResolveSkillType(skillConfig.Type, out var skillType) == false)
            {
                _logger.LogWarning($"[Story][Battle]: Skill unknown type, id = {trimmed}, type = {skillConfig.Type}");

                return new UnknownSkill(new SkillMapper(skillConfig.Id, skillConfig.Type, SkillType.Unknown, skillConfig.Parameters));
            }

            var mapper = new SkillMapper(skillConfig.Id, skillConfig.Type, skillType, skillConfig.Parameters);

            return CreateSkill(skillType, mapper);
        }

        public bool IsKnownSkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
                return false;

            return TryResolveSkillType(skillConfig.Type, out _);
        }

        private bool TryFindSkillConfig(string skillId, [MaybeNullWhen(false)] out Server.Skills.ISkillMapper skillConfig)
        {
            if (int.TryParse(skillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                && _configDistributor.Skills.TryGet(numericId, out skillConfig))
                return true;

            return _configDistributor.Skills.TryGetByType(skillId, out skillConfig);
        }

        private bool TryResolveSkillType(string skillTypeKey, out SkillType skillType)
        {
            if (string.Equals(skillTypeKey, "fireball", StringComparison.OrdinalIgnoreCase))
            {
                skillType = SkillType.Fireball;

                return true;
            }

            if (string.Equals(skillTypeKey, "energy", StringComparison.OrdinalIgnoreCase))
            {
                skillType = SkillType.Energy;

                return true;
            }

            if (string.Equals(skillTypeKey, "summon_1_skill_1", StringComparison.OrdinalIgnoreCase))
            {
                skillType = SkillType.VenomStrike;

                return true;
            }

            if (string.Equals(skillTypeKey, "summon_2_skill_1", StringComparison.OrdinalIgnoreCase))
            {
                skillType = SkillType.WarHowl;

                return true;
            }

            if (string.Equals(skillTypeKey, "summon_3_skill_1", StringComparison.OrdinalIgnoreCase))
            {
                skillType = SkillType.SoulSiphon;

                return true;
            }

            skillType = SkillType.Unknown;

            return false;
        }

        private ISkill CreateSkill(SkillType skillType, ISkillMapper mapper)
        {
            switch (skillType)
            {
                case SkillType.Fireball:
                    return new FireballSkill(mapper);
                case SkillType.Energy:
                    return new EnergySkill(mapper);
                case SkillType.VenomStrike:
                    return new SummonVenomStrikeSkill(mapper);
                case SkillType.WarHowl:
                    return new SummonWarHowlSkill(mapper);
                case SkillType.SoulSiphon:
                    return new SummonSoulSiphonSkill(mapper);
                default:
                    return new UnknownSkill(mapper);
            }
        }
    }
}
