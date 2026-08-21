namespace Server.Battles
{
    internal interface ITeamSnapshot
    {
        public List<IUnitSnapshot> MainUnits { get; set; }

        public List<IUnitSnapshot> Summons { get; set; }
    }
}
