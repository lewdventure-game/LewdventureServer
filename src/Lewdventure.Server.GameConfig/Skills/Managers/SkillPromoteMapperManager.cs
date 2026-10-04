using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Skills
{
    internal sealed class SkillPromoteMapperManager : BaseManager<ISkillPromoteMapper>, ISkillPromoteMapperManager
    {
        public bool TryGet(int patternId, int level, [MaybeNullWhen(false)] out ISkillPromoteMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.PatternId != patternId || currentMapper.Level != level)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
