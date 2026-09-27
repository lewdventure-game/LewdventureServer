namespace Server.Battles
{
    public interface ITeamSnapshot
    {
        public List<IUnitSnapshot> MainUnits { get; set; }

        public List<IUnitSnapshot> Summons { get; set; }
    }
}
