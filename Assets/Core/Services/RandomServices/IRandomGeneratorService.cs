namespace Server.Services
{
    internal interface IRandomGeneratorService
    {
        public float GetRandomValue();

        public int Range(int minInclusive, int maxExclusive);

        public float Range(float minInclusive, float maxInclusive);

        public T Choice<T>(T[] options);
    }
}
