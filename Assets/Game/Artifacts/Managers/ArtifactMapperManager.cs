using Core.Collections;

namespace Server.Artifacts
{
    internal sealed class ArtifactMapperManager : BaseManager<IArtifactMapper>, IArtifactMapperManager
    {
        public bool TryGet(int artifactId, out IArtifactMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != artifactId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
