using Core.Collections;

namespace Server.Artifacts
{
    internal interface IArtifactMapperManager : IManager<IArtifactMapper>
    {
        public bool TryGet(int artifactId, out IArtifactMapper mapper);
    }
}
