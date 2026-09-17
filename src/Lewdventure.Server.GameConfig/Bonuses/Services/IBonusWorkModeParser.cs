namespace Server.Bonuses
{
    internal interface IBonusWorkModeParser
    {
        public BonusWorkMode Parse(string raw);
    }
}
