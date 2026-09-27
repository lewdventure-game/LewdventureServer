using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Stories
{
    public interface IStoryStageMapperManager : IManager<IStoryStageMapper>
    {
        public bool TryGet(int storyStageId, [MaybeNullWhen(false)] out IStoryStageMapper mapper);
    }
}
