using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Server.Configs;
using Server.Services;

namespace Server.Battles
{
    internal sealed class SkillFactory : ISkillFactory
    {
        private readonly ICoreLog _coreLog;
        private readonly IConfigDistributor _configDistributor;
        private readonly ISkillActionRegistry _skillActionRegistry;
        private readonly ISkillArgumentReader _skillArgumentReader;
        private readonly ISkillDefinitionParser _skillDefinitionParser;
        private readonly ISkillTargetResolver _skillTargetResolver;
        private readonly ISkillTriggerRegistry _skillTriggerRegistry;

        public SkillFactory(
            ICoreLog coreLog,
            IConfigDistributor configDistributor,
            ISkillActionRegistry skillActionRegistry,
            ISkillArgumentReader skillArgumentReader,
            ISkillDefinitionParser skillDefinitionParser,
            ISkillTargetResolver skillTargetResolver,
            ISkillTriggerRegistry skillTriggerRegistry)
        {
            _coreLog = coreLog;
            _configDistributor = configDistributor;
            _skillActionRegistry = skillActionRegistry;
            _skillArgumentReader = skillArgumentReader;
            _skillDefinitionParser = skillDefinitionParser;
            _skillTargetResolver = skillTargetResolver;
            _skillTriggerRegistry = skillTriggerRegistry;
        }

        public ISkill Create(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                _coreLog.Warning($"[Story][Battle]: Skill unknown, id = {skillId}");

                return new UnknownSkill(0, string.Empty);
            }

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
            {
                _coreLog.Warning($"[Story][Battle]: Skill unknown, id = {trimmed}");

                return new UnknownSkill(0, trimmed);
            }

            if (_skillDefinitionParser.TryParse(skillConfig, out var definition))
            {
                _coreLog.Debug($"[Story][Battle]: Skill built from triggers and actions, skillId = {skillConfig.Id}, triggers = {definition.Triggers.Count}, actions = {definition.Actions.Count}");

                return new ConfiguredSkill(
                    definition,
                    trimmed,
                    _coreLog,
                    _skillActionRegistry,
                    _skillArgumentReader,
                    _skillTargetResolver,
                    _skillTriggerRegistry);
            }

            _coreLog.Warning($"[Story][Battle]: Skill without triggers and actions is not supported, id = {trimmed}, type = {skillConfig.Type}");

            return new UnknownSkill(skillConfig.Id, trimmed);
        }

        public bool IsKnownSkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
                return false;

            return _skillDefinitionParser.TryParse(skillConfig, out _);
        }

        private bool TryFindSkillConfig(string skillId, [MaybeNullWhen(false)] out Server.Skills.ISkillMapper skillConfig)
        {
            if (int.TryParse(skillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                && _configDistributor.Skills.TryGet(numericId, out skillConfig))
                return true;

            return _configDistributor.Skills.TryGetByType(skillId, out skillConfig);
        }
    }
}
