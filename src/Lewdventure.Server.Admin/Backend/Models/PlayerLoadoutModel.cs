namespace Server.Admin.Backend.Models
{
    public sealed class PlayerLoadoutModel
    {
        public int CharacterId { get; set; }

        public Dictionary<string, string> Equipment { get; set; } = new();

        public List<int> Summons { get; set; } = new();
    }
}
