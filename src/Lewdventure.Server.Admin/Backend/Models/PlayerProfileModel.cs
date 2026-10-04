namespace Server.Admin.Backend.Models
{
    public sealed class PlayerProfileModel
    {
        public string UserId { get; set; } = string.Empty;

        public long Rev { get; set; }

        public Dictionary<string, long> Resources { get; set; } = new();

        public List<PlayerUnitModel> Characters { get; set; } = new();

        public List<PlayerUnitModel> Summons { get; set; } = new();

        public List<PlayerEquipmentModel> Equipment { get; set; } = new();

        public PlayerLoadoutModel Loadout { get; set; } = new();

        public PlayerStoryModel Story { get; set; } = new();

        public Dictionary<string, int> Flags { get; set; } = new();
    }
}
