using Core.Collections;

namespace Server.Trainings
{
    internal interface ITrainingMapperManager : IManager<ITrainingMapper>
    {
        public bool TryGet(int trainingLevel, out ITrainingMapper mapper);
    }
}
