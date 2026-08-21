namespace Server.Battles
{
    internal enum BattlePhaseType
    {
        Unknown = 0,
        StatusTrigger = 1,
        PerkTrigger = 2,
        SummonSkill = 3,
        SummonAttack = 4,
        UnitSkill = 5,
        NormalAttack = 6,
        CounterAttack = 7,
        Combo1Attack = 8,
        Combo2Attack = 9,
        EnergySkill = 10,
        Death = 11,
        ReturnToPosition = 12,
        Approach = 13,
        UnitCooldown = 14,
        SummonCooldown = 15,
        CounterCooldown = 16,
        ComboCooldown = 17,
    }
}
