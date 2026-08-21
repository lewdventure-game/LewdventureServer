using Core.Collections;

namespace Server.Configs
{
    internal interface IConstantsMapperManager : IManager<IConstantsMapper>
    {
        public bool TryGet(string constantName, out IConstantsMapper mapper);
    }
}
