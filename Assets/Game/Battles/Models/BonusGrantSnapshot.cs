namespace Server.Battles
{
    internal sealed class BonusGrantSnapshot : IBonusGrantSnapshot
    {
        public int Id { get; set; }

        public int Count { get; set; }

        public int RemainingBattles { get; set; }
    }
}
