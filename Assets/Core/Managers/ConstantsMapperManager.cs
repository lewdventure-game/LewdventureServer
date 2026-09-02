using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Configs
{
    internal sealed class ConstantsMapperManager : BaseManager<IConstantsMapper>, IConstantsMapperManager
    {
        public bool TryGet(string constantName, [MaybeNullWhen(false)] out IConstantsMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.ConstantName == constantName)
                {
                    mapper = currentMapper;

                    return true;
                }
            }

            mapper = null;

            return false;
        }
    }
}
