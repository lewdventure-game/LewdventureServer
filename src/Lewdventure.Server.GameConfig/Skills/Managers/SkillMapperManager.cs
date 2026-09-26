using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Skills
{
    internal sealed class SkillMapperManager : BaseManager<ISkillMapper>, ISkillMapperManager
    {
        public bool TryGet(int skillId, [MaybeNullWhen(false)] out ISkillMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != skillId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }

        public bool TryGetByType(string skillType, [MaybeNullWhen(false)] out ISkillMapper mapper)
        {
            if (string.IsNullOrWhiteSpace(skillType))
            {
                mapper = null;

                return false;
            }

            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (string.Equals(currentMapper.Type, skillType, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
