using System.Diagnostics.CodeAnalysis;
using Core.Collections;

namespace Server.Trainings
{
    public interface ITrainingMapperManager : IManager<ITrainingMapper>
    {
        public bool TryGet(int trainingLevel, [MaybeNullWhen(false)] out ITrainingMapper mapper);
    }
}
