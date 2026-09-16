namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigSheetCatalog
    {
        private readonly List<ConfigSheetDefinition> _sheets = new()
        {
            new ConfigSheetDefinition("Constants", "1LIw9xcJQmsn4GThnsLQITpRLJ_6avKv-wTBj0MpsVNM", "B:E"),
            new ConfigSheetDefinition("Characters", "1rB22U8FrboY1hHqg1AwhEvDBzb1OxK9JTUWZ_1isRTg", "B:J"),
            new ConfigSheetDefinition("Bonuses", "1jrzVDp9dTRtJBjyFFP1adtkbcqMEHIfnWs3XsJuR2ac", "B:G"),
            new ConfigSheetDefinition("Statuses", "1Dwm4eRVQmLegaxulMk2RdNeOT7GlGu6QORQqeKnuUiY", "B:H"),
            new ConfigSheetDefinition("Summons", "1QstDNh059XftqtZcIdIL3o80o_g8qQs_akKFe5jChCk", "B:J"),
            new ConfigSheetDefinition("Summon_levels", "18swHo4NuLgys6_oGk_zqqpwadSm94KsBm-OoJIdoxR8", "B:I"),
            new ConfigSheetDefinition("Mastery", "1grplwUHMfdcs0-0QvFZvhQqsrcS4ywwe6nTQsEB6EOg", "B:H"),
            new ConfigSheetDefinition("Enemies", "1GLSin50lIGoTOZbmV3OQU8TXB_XAdUcnsnsfBK0AsKk", "B:Q"),
            new ConfigSheetDefinition("Equipments", "1XP47_4sQ5uK2_6rGSBH4bkWgXVDBLfiYIYtVk9qIB0E", "B:R"),
            new ConfigSheetDefinition("Story_levels", "1gz8t6fmWwIwvz93U7pKZ8rrBu9RdfyJB5Yn9G2ITuCE", "B:I"),
            new ConfigSheetDefinition("Story_stages", "1csDHVX7F0bStlHAayJV_3TOLOoyIbbCdg4O3ORTpx8g", "B:G"),
            new ConfigSheetDefinition("Story_events", "1chWPFzItT87MzdrEQAp7DvAkhEiY_xuLwor42fyFGDk", "B:G"),
            new ConfigSheetDefinition("Exp_levels_patterns", "1YC9UR3RLOU-0r14ovkXeR_Dp1dtkkJCX_veZxLSBmVM", "B:G"),
            new ConfigSheetDefinition("Perks", "1UyW7R_DZHDTiDtZoicZQ9GhDctVaK_zA7C0aBP0T6Hk", "B:J"),
            new ConfigSheetDefinition("Perk_groups", "1USA6a-252oCKSmCqUDMtQn78pMiubIIj2garPm0pyBU", "B:G"),
        };

        public IReadOnlyList<ConfigSheetDefinition> Sheets => _sheets;
    }
}
