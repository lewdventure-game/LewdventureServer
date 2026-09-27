namespace Server.Services
{
    internal interface ISeededRandomFactory
    {
        public ISeededRandomService Create();

        public ISeededRandomService Create(ulong seed);
    }
}
