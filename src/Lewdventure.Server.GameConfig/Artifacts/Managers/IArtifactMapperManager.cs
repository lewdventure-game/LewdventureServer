using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Artifacts
{
    public interface IArtifactMapperManager : IManager<IArtifactMapper>
    {
        public bool TryGet(int artifactId, [MaybeNullWhen(false)] out IArtifactMapper mapper);
    }
}
