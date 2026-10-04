using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Skills
{
    public interface ISkillPromoteMapperManager : IManager<ISkillPromoteMapper>
    {
        public bool TryGet(int patternId, int level, [MaybeNullWhen(false)] out ISkillPromoteMapper mapper);
    }
}
