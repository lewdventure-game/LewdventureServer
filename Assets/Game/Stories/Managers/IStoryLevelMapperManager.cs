using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryLevelMapperManager : IManager<IStoryLevelMapper>
    {
        public bool TryGet(int storyId, out IStoryLevelMapper storyLevelMapper);
    }
}
