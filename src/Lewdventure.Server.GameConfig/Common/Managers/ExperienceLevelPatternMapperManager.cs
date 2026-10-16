using System.Diagnostics.CodeAnalysis;
using Server.Collections;
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

        public bool TryGetOrLast(int patternId, int experienceLevel, [MaybeNullWhen(false)] out IExperienceLevelPatternMapper mapper)
        {
            if (TryGet(patternId, experienceLevel, out mapper))
                return true;

            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != patternId || experienceLevel < currentMapper.ExperienceLevel)
                    continue;

                if (mapper == null || mapper.ExperienceLevel < currentMapper.ExperienceLevel)
                    mapper = currentMapper;
            }

            return mapper != null;
        }
    }
}
