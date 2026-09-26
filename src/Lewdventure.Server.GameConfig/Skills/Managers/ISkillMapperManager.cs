using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Skills
{
    internal interface ISkillMapperManager : IManager<ISkillMapper>
    {
        public bool TryGet(int skillId, [MaybeNullWhen(false)] out ISkillMapper mapper);

        public bool TryGetByType(string skillType, [MaybeNullWhen(false)] out ISkillMapper mapper);
    }
}
