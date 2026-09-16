using System.Diagnostics.CodeAnalysis;
using Core.Collections;
using Server.Configs;

namespace Server.Common
{
    internal sealed class ExperienceLevelPatternMapperManager : BaseManager<IExperienceLevelPatternMapper>, IExperienceLevelPatternMapperManager
    {
        public bool TryGet(int patternId, int experienceLevel, [MaybeNullWhen(false)] out IExperienceLevelPatternMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != patternId || currentMapper.ExperienceLevel != experienceLevel)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
