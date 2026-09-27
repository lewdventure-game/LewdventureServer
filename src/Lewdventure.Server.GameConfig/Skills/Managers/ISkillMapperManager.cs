using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Skills
{
    public interface ISkillMapperManager : IManager<ISkillMapper>
    {
        public bool TryGet(int skillId, [MaybeNullWhen(false)] out ISkillMapper mapper);

        public bool TryGetByType(string skillType, [MaybeNullWhen(false)] out ISkillMapper mapper);
    }
}
