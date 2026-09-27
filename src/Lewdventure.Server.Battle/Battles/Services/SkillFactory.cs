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
        private readonly ParserUtils _parserUtils;
        private readonly Dictionary<SkillType, ISkillCreator> _creatorsByType = new();
        private readonly Dictionary<string, ISkillCreator> _creatorsByTypeKey = new(StringComparer.OrdinalIgnoreCase);

        public SkillFactory(
            ICoreLog coreLog,
            IConfigDistributor configDistributor,
            ParserUtils parserUtils,
            IReadOnlyList<ISkillCreator> skillCreators)
        {
            _coreLog = coreLog;
            _configDistributor = configDistributor;
            _parserUtils = parserUtils;

            for (int i = 0; i < skillCreators.Count; i++)
            {
                var creator = skillCreators[i];

                _creatorsByType[creator.SkillType] = creator;
                _creatorsByTypeKey[creator.TypeKey] = creator;
            }
        }

        public ISkill Create(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                _coreLog.Warning($"[Story][Battle]: Skill unknown, id = {skillId}");

                return new UnknownSkill(new SkillMapper(0, string.Empty, SkillType.Unknown, string.Empty), _parserUtils);
            }

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
            {
                _coreLog.Warning($"[Story][Battle]: Skill unknown, id = {trimmed}");

                return new UnknownSkill(new SkillMapper(0, trimmed, SkillType.Unknown, string.Empty), _parserUtils);
            }

            if (TryResolveCreator(skillConfig.Type, out var creator) == false)
            {
                _coreLog.Warning($"[Story][Battle]: Skill unknown type, id = {trimmed}, type = {skillConfig.Type}");

                return new UnknownSkill(new SkillMapper(skillConfig.Id, skillConfig.Type, SkillType.Unknown, skillConfig.Parameters), _parserUtils);
            }

            var mapper = new SkillMapper(skillConfig.Id, skillConfig.Type, creator.SkillType, skillConfig.Parameters);

            return creator.Create(mapper);
        }

        public IReadOnlyCollection<string> KnownTypeKeys => _creatorsByTypeKey.Keys;

        public bool IsKnownSkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            var trimmed = skillId.Trim();

            if (TryFindSkillConfig(trimmed, out var skillConfig) == false)
                return false;

            return TryResolveCreator(skillConfig.Type, out _);
        }

        private bool TryFindSkillConfig(string skillId, [MaybeNullWhen(false)] out Server.Skills.ISkillMapper skillConfig)
        {
            if (int.TryParse(skillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                && _configDistributor.Skills.TryGet(numericId, out skillConfig))
                return true;

            return _configDistributor.Skills.TryGetByType(skillId, out skillConfig);
        }

        private bool TryResolveCreator(string skillTypeKey, [MaybeNullWhen(false)] out ISkillCreator creator)
        {
            if (string.IsNullOrWhiteSpace(skillTypeKey))
            {
                creator = null;

                return false;
            }

            return _creatorsByTypeKey.TryGetValue(skillTypeKey.Trim(), out creator);
        }
    }
}
