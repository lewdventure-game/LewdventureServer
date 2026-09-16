using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryStageMapperManager : IManager<IStoryStageMapper>
    {
        public bool TryGet(int storyStageId, [MaybeNullWhen(false)] out IStoryStageMapper mapper);
    }
}
