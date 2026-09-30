namespace Server.Api.Endpoints
{
    internal sealed class PlayerProfileResponse
    {
        public string UserId { get; set; } = string.Empty;

        public long Rev { get; set; }

        public Dictionary<string, long> Resources { get; set; } = new();

        public List<PlayerCharacterResponse> Characters { get; set; } = new();

        public List<PlayerSummonResponse> Summons { get; set; } = new();

        public List<PlayerEquipmentResponse> Equipment { get; set; } = new();

        public List<PlayerBonusResponse> Bonuses { get; set; } = new();

        public PlayerLoadoutResponse Loadout { get; set; } = new();

        public PlayerStoryResponse Story { get; set; } = new();

        public Dictionary<string, int> Flags { get; set; } = new();
    }
}
