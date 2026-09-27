namespace Server.Services
{
    internal sealed class SeededRandomFactory : ISeededRandomFactory
    {
        public ISeededRandomService Create()
        {
            return new SeededRandomService();
        }

        public ISeededRandomService Create(ulong seed)
        {
            return new SeededRandomService(seed);
        }
    }
}
