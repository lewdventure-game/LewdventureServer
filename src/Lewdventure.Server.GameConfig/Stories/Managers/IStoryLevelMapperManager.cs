using System.Diagnostics.CodeAnalysis;
using Server.Collections;

namespace Server.Stories
{
    public interface IStoryLevelMapperManager : IManager<IStoryLevelMapper>
    {
        public bool TryGet(int storyId, [MaybeNullWhen(false)] out IStoryLevelMapper storyLevelMapper);
    }
}
