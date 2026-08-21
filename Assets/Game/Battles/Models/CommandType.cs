namespace Server.Battles
{
    internal enum CommandType
    {
        Unknown = 0,
        Wait = 1,
        Approach = 2,
        ReturnToPosition = 3,
        PlayAnimation = 4,
        ShowDamage = 5,
        ShowHeal = 6,
        ShowMiss = 7,
        ApplyStatus = 8,
        RemoveStatus = 9,
        TickStatus = 10,
        CastSkill = 11,
        TriggerPerk = 12,
        SetHp = 13,
        SetEnergy = 14,
        SetBonus = 15,
        SpawnUnit = 16,
        DespawnUnit = 17,
        KillUnit = 18,
        SetBattleResult = 19,
        GrantReward = 20,
    }
}
