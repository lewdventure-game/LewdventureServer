using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Stories
{
    internal interface IStoryLevelMapperManager : IManager<IStoryLevelMapper>
    {
        public bool TryGet(int storyId, [MaybeNullWhen(false)] out IStoryLevelMapper storyLevelMapper);
    }
}
