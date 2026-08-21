using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryEventMapperManager : IManager<IStoryEventMapper>
    {
        public bool TryGet(int storyEventId, out IStoryEventMapper mapper);
    }
}
