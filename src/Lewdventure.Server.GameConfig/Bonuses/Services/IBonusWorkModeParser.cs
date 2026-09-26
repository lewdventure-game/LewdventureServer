namespace Server.Bonuses
{
    internal interface IBonusWorkModeParser
    {
        public bool TryParse(string raw, out BonusWorkMode workMode);
    }
}
