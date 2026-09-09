namespace Server.Battles
{
    internal interface IBonusGrantSnapshot
    {
        public int Id { get; set; }

        public int Count { get; set; }

        public int RemainingBattles { get; set; }
    }
}
