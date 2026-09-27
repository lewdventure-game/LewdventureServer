using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Stories
{
    internal sealed class StoryStageMapperManager : BaseManager<IStoryStageMapper>, IStoryStageMapperManager
    {
        public bool TryGet(int storyStageId, [MaybeNullWhen(false)] out IStoryStageMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Id != storyStageId)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
