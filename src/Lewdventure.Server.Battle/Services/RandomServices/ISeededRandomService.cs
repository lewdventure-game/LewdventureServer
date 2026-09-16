namespace Server.Services
{
    internal interface ISeededRandomService : IRandomGeneratorService
    {
        public ulong Seed { get; }

        public void SetSeed(ulong seed);

        public ulong NextULong();

        public uint NextUInt();
    }
}
