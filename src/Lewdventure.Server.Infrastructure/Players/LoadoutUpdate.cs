namespace Server.Infrastructure.Players
{
    internal sealed class LoadoutUpdate
    {
        public LoadoutUpdate(int characterId, IReadOnlyDictionary<string, string> equipment, IReadOnlyList<int> summons)
        {
            CharacterId = characterId;
            Equipment = equipment;
            Summons = summons;
        }

        public int CharacterId { get; }

        public IReadOnlyDictionary<string, string> Equipment { get; }

        public IReadOnlyList<int> Summons { get; }
    }
}
