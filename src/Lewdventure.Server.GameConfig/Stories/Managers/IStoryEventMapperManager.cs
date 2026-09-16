using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryEventMapperManager : IManager<IStoryEventMapper>
    {
        public bool TryGet(int storyEventId, [MaybeNullWhen(false)] out IStoryEventMapper mapper);
    }
}
