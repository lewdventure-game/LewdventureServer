using Core.Collections;

namespace Server.Stories
{
    internal sealed class StoryLevelMapperManager : BaseManager<IStoryLevelMapper>, IStoryLevelMapperManager
    {
        public bool TryGet(int storyId, out IStoryLevelMapper storyLevelMapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var mapper = Collection[i];

                if (mapper.Id != storyId)
                    continue;

                storyLevelMapper = mapper;

                return true;
            }

            storyLevelMapper = null;

            return false;
        }
    }
}
