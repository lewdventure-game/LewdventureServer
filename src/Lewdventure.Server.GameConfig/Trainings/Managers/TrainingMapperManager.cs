using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Trainings
{
    internal sealed class TrainingMapperManager : BaseManager<ITrainingMapper>, ITrainingMapperManager
    {
        public bool TryGet(int trainingLevel, [MaybeNullWhen(false)] out ITrainingMapper mapper)
        {
            for (int i = 0; i < Collection.Count; i++)
            {
                var currentMapper = Collection[i];

                if (currentMapper.Level != trainingLevel)
                    continue;

                mapper = currentMapper;

                return true;
            }

            mapper = null;

            return false;
        }
    }
}
