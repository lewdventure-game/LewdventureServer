namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerLoadoutDocument
    {
        public int CharacterId { get; set; }

        public Dictionary<string, string> Equipment { get; set; } = new();

        public List<int> Summons { get; set; } = new();
    }
}
