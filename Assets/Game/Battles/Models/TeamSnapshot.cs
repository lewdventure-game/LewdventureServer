namespace Server.Battles
{
    internal sealed class TeamSnapshot : ITeamSnapshot
    {
        public List<IUnitSnapshot> MainUnits { get; set; } = new();

        public List<IUnitSnapshot> Summons { get; set; } = new();
    }
}
