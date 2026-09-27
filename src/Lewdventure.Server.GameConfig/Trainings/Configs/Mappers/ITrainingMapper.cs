using Server.Configs;

namespace Server.Trainings
{
    public interface ITrainingMapper : IConfigMapper
    {
        public int Level { get; }

        public int[] BonusIds { get; }

        public float[] BonusValues { get; }
    }
}
