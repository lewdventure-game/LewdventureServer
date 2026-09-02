using System.Diagnostics.CodeAnalysis;
using Core.Collections;
using Server.Configs;

namespace Server.Common
{
    internal interface IExperienceLevelPatternMapperManager : IManager<IExperienceLevelPatternMapper>
    {
        public bool TryGet(int patternId, int experienceLevel, [MaybeNullWhen(false)] out IExperienceLevelPatternMapper mapper);
    }
}
