using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Server.Battles
{
    internal sealed class SkillFactory : ISkillFactory
    {
        private readonly ILogger<SkillFactory> _logger;

        public SkillFactory(ILogger<SkillFactory> logger)
        {
            _logger = logger;
        }

        public ISkill Create(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                _logger.LogWarning($"[Story][Battle] skill unknown id = ");

                return new UnknownSkill(new SkillMapper(0, string.Empty, SkillType.Unknown, string.Empty));
            }

            var trimmed = skillId.Trim();

            if (TryResolveFireball(trimmed, out var fireballKey))
                return new FireballSkill(new SkillMapper((int)SkillType.Fireball, fireballKey, SkillType.Fireball, "1;1"));

            if (TryResolveEnergy(trimmed))
                return new EnergySkill(new SkillMapper((int)SkillType.Energy, "energy", SkillType.Energy, string.Empty));

            if (string.Equals(trimmed, "summon_1_skill_1", StringComparison.Ordinal))
                return new SummonVenomStrikeSkill(new SkillMapper((int)SkillType.VenomStrike, trimmed, SkillType.VenomStrike, string.Empty));

            if (string.Equals(trimmed, "summon_2_skill_1", StringComparison.Ordinal))
                return new SummonWarHowlSkill(new SkillMapper((int)SkillType.WarHowl, trimmed, SkillType.WarHowl, string.Empty));

            if (string.Equals(trimmed, "summon_3_skill_1", StringComparison.Ordinal))
                return new SummonSoulSiphonSkill(new SkillMapper((int)SkillType.SoulSiphon, trimmed, SkillType.SoulSiphon, string.Empty));

            _logger.LogWarning($"[Story][Battle] skill unknown id = {trimmed}");

            return new UnknownSkill(new SkillMapper(0, trimmed, SkillType.Unknown, string.Empty));
        }

        public bool IsKnownSkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            var trimmed = skillId.Trim();

            if (TryResolveFireball(trimmed, out _))
                return true;

            if (TryResolveEnergy(trimmed))
                return true;

            if (string.Equals(trimmed, "summon_1_skill_1", StringComparison.Ordinal))
                return true;

            if (string.Equals(trimmed, "summon_2_skill_1", StringComparison.Ordinal))
                return true;

            if (string.Equals(trimmed, "summon_3_skill_1", StringComparison.Ordinal))
                return true;

            return false;
        }

        private bool TryResolveFireball(string skillId, out string skillKey)
        {
            skillKey = string.Empty;

            if (string.Equals(skillId, "fireball", StringComparison.OrdinalIgnoreCase))
            {
                skillKey = "fireball";

                return true;
            }

            if (int.TryParse(skillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId) == false)
                return false;

            if (numericId != (int)SkillType.Fireball)
                return false;

            skillKey = skillId;

            return true;
        }

        private bool TryResolveEnergy(string skillId)
        {
            if (string.Equals(skillId, "energy", StringComparison.OrdinalIgnoreCase))
                return true;

            if (int.TryParse(skillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId) == false)
                return false;

            return numericId == (int)SkillType.Energy;
        }
    }
}
