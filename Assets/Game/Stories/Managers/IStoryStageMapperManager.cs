using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryStageMapperManager : IManager<IStoryStageMapper>
    {
        public bool TryGet(int storyStageId, out IStoryStageMapper mapper);
    }
}
