using Core.Collections;

namespace Server.Stories
{
    internal sealed class StoryEventMapperManager : BaseManager<IStoryEventMapper>, IStoryEventMapperManager
    {
        public bool TryGet(int storyEventId, out IStoryEventMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != storyEventId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
