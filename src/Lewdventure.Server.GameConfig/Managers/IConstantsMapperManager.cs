using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Configs
{
    public interface IConstantsMapperManager : IManager<IConstantsMapper>
    {
        public bool TryGet(string constantName, [MaybeNullWhen(false)] out IConstantsMapper mapper);
    }
}
